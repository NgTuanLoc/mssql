using System.Globalization;
using EfCoreLessons.Lab;
using EfCoreLessons.Model;

namespace EfCoreLessons.E01;

public record EmployeeTitle(int BusinessEntityId, string JobTitle);
public record ProductPrice(int ProductId, string Name, decimal ListPrice);
public record ProductLabel(string Name, string Price);
public record ProductRef(int ProductId, string Name);

// The checks for E01. E01Exercises (yours) and E01Solutions both inherit them.
public abstract class E01Checks : LabTest
{
    protected abstract Task<List<EmployeeTitle>> Ex1(AdventureWorksContext db);
    protected abstract Task<List<ProductPrice>> Ex2(AdventureWorksContext db, int page, int pageSize);
    protected abstract Task<bool> Ex3(AdventureWorksContext db, decimal minPrice);
    protected abstract string Ex4(AdventureWorksContext db, int customerId);
    protected abstract Task<List<ProductLabel>> Ex5(AdventureWorksContext db, decimal minPrice);
    protected abstract List<string> Ch1(AdventureWorksContext db);
    protected abstract Task<ProductRef?> Ch2(AdventureWorksContext db, string productNumberPrefix);
    protected abstract Task<int> Ch3(AdventureWorksContext db, byte status);

    [Exercise]
    public async Task Ex1_Managers()
    {
        var result = await Ex1(Db);

        Assert.Equal(await Reference.Rows<EmployeeTitle>($"""
            SELECT BusinessEntityID AS BusinessEntityId, JobTitle
            FROM HumanResources.Employee
            WHERE JobTitle LIKE '%Manager%'
            ORDER BY JobTitle, BusinessEntityID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.DoesNotContain("[BirthDate]", Sql.All); // only the columns you return
    }

    [Exercise]
    public async Task Ex2_DeterministicPaging()
    {
        var result = await Ex2(Db, page: 3, pageSize: 10);

        Assert.Equal(await Reference.Rows<ProductPrice>($"""
            SELECT ProductID AS ProductId, Name, ListPrice
            FROM Production.Product
            WHERE ListPrice > 0
            ORDER BY ListPrice DESC, ProductID
            OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("OFFSET", Sql.All);                                         // paged in SQL
        Assert.Contains("ORDER BY [p].[ListPrice] DESC, [p].[ProductID]", Sql.All); // tie-breaker
    }

    [Exercise]
    public async Task Ex3_AnyAbovePrice()
    {
        Assert.True(await Ex3(Db, 3000m));
        Assert.False(await Ex3(Db, 4000m));

        Sql.AssertRoundTrips(2);
        Assert.Contains("EXISTS", Sql.All);
        Assert.DoesNotContain("COUNT", Sql.All);
    }

    [Exercise]
    public void Ex4_SqlWithoutRunningIt()
    {
        var sql = Ex4(Db, 29825);

        Assert.Equal(0, Sql.Count);                 // ToQueryString never touches the database
        Assert.Contains("DECLARE @", sql);
        Assert.Contains("29825", sql);
        Assert.Contains("[CustomerID] = @", sql);   // a parameter, not an inlined literal
        Assert.Contains("TOP(", sql);
        Assert.Contains("ORDER BY [s].[OrderDate] DESC", sql);
    }

    [Exercise]
    public async Task Ex5_FormattedPrices()
    {
        var result = await Ex5(Db, 3000m);

        var us = CultureInfo.GetCultureInfo("en-US");
        var expected = (await Reference.Rows<ProductPrice>($"""
                SELECT ProductID AS ProductId, Name, ListPrice
                FROM Production.Product
                WHERE ListPrice > 3000
                ORDER BY ListPrice DESC, Name
                """))
            .Select(p => new ProductLabel(p.Name, p.ListPrice.ToString("C", us)))
            .ToList();
        Assert.Equal(expected, result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("WHERE", Sql.All);                 // filtered in SQL
        Assert.DoesNotContain("[ProductNumber]", Sql.All); // only the columns the labels need
    }

    [Exercise]
    public async Task Ch1_TablesWithTriggers()
    {
        var result = Ch1(Db);

        var expected = (await Reference.Rows<string>($"""
                SELECT DISTINCT OBJECT_SCHEMA_NAME(parent_id) + '.' + OBJECT_NAME(parent_id) AS Value
                FROM sys.triggers
                WHERE parent_class = 1
                  AND OBJECT_SCHEMA_NAME(parent_id) IN ('Person', 'HumanResources', 'Production', 'Purchasing', 'Sales', 'dbo')
                """))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(expected, result);
        Assert.Equal(0, Sql.Count); // answered from the model, not a query
    }

    [Exercise]
    public async Task Ch2_ExactlyOneByPrefix()
    {
        Assert.Equal(new ProductRef(782, "Mountain-200 Black, 38"), await Ch2(Db, "BK-M68B-38"));
        Assert.Null(await Ch2(Db, "BK_M68B-38")); // '_' is literal here, not a LIKE wildcard
        await Assert.ThrowsAsync<InvalidOperationException>(() => Ch2(Db, "BK-M68B")); // three matches

        Sql.AssertRoundTrips(3);
        Assert.All(Sql.Commands, sql => Assert.Contains("TOP(2)", sql));
    }

    [Exercise]
    public async Task Ch3_PlanPerStatus()
    {
        var counts = new List<int>();
        for (byte status = 1; status <= 6; status++)
            counts.Add(await Ch3(Db, status));

        Assert.Equal([0, 0, 0, 0, 31465, 0], counts);      // every AdventureWorks order is Status 5 (shipped)
        Assert.Equal(6, Sql.Commands.Distinct().Count());  // six SQL texts: one plan per value
        Assert.DoesNotContain("@", Sql.All);               // the status is inlined, not a parameter
    }
}
