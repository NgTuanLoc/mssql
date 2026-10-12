using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E04;

public record PersonName(int BusinessEntityId, string FirstName, string LastName);
public record CustomerOrderTotal(int CustomerId, int SalesOrderId, DateTime OrderDate, decimal TotalDue, decimal RunningTotal);
public record OrderRunningTotal(int SalesOrderId, DateTime OrderDate, decimal TotalDue, decimal RunningTotal);
public record PersonContact(int PersonId, string? JobTitle, string? BusinessEntityType);
public record ProductStock(int ProductId, string Name, int Stock);
public record ManagerName(int RecursionLevel, string ManagerFirstName, string ManagerLastName);
public record ProductPrice(int ProductId, decimal ListPrice);
public record BomComponent(int ComponentId, int Level);
public record ProductRef(int ProductId, string Name);

// The checks for E04. E04Exercises (yours) and E04Solutions both inherit them.
// This lesson's contexts are E04Context: the shared model plus two function mappings.
public abstract class E04Checks : LabTest<E04Context>
{
    protected abstract Task<List<PersonName>> Ex1(E04Context db, string lastName);
    protected abstract Task<List<OrderRunningTotal>> Ex2(E04Context db, int customerId);
    protected abstract Task<List<PersonContact>> Ex3(E04Context db, List<int> personIds);
    protected abstract Task<List<ProductStock>> Ex4(E04Context db, int top);
    protected abstract Task<List<string>> Ex5(E04Context db, int employeeId);
    protected abstract Task<List<ProductPrice>> Ch1(E04Context db, int subcategoryId, decimal factor);
    protected abstract Task<List<BomComponent>> Ch2(E04Context db, int productId, int minLevel);
    protected abstract Task<List<ProductRef>> Ch3(E04Context db, string sortColumn);

    [Exercise]
    public async Task Ex1_SoundsLike()
    {
        Assert.Equal(await Reference.Rows<PersonName>($"""
            SELECT BusinessEntityID AS BusinessEntityId, FirstName, LastName
            FROM Person.Person
            WHERE SOUNDEX(LastName) = SOUNDEX({"Smyth"})
            ORDER BY LastName, FirstName, BusinessEntityID
            """), await Ex1(Db, "Smyth"));

        var hostile = "x' OR 1=1 --";
        Assert.Equal(await Reference.Rows<PersonName>($"""
            SELECT BusinessEntityID AS BusinessEntityId, FirstName, LastName
            FROM Person.Person
            WHERE SOUNDEX(LastName) = SOUNDEX({hostile})
            ORDER BY LastName, FirstName, BusinessEntityID
            """), await Ex1(Db, hostile)); // the input stays data, never SQL

        Sql.AssertRoundTrips(2);
        Assert.All(Sql.Commands, sql => Assert.Contains("SOUNDEX", sql));
        Assert.DoesNotContain("OR 1=1", Sql.All);
    }

    [Exercise]
    public async Task Ex2_RunningTotalInSql()
    {
        var result = await Ex2(Db, 11091);

        Assert.Equal(await Reference.Rows<OrderRunningTotal>($"""
            SELECT SalesOrderID AS SalesOrderId, OrderDate, TotalDue,
                   SUM(TotalDue) OVER (ORDER BY OrderDate, SalesOrderID ROWS UNBOUNDED PRECEDING) AS RunningTotal
            FROM Sales.SalesOrderHeader
            WHERE CustomerID = {11091}
            ORDER BY OrderDate, SalesOrderID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("OVER", Sql.All);         // computed by SQL Server
        Assert.Contains("PARTITION BY", Sql.All); // ...for every customer,
        Assert.Contains("FROM (", Sql.All);       // ...with the customer filter composed on top in LINQ
    }

    [Exercise]
    public async Task Ex3_ContactsViaFunction()
    {
        var ids = new List<int> { 1, 291, 1491, 1699 }; // an employee, a store contact, a vendor contact, a consumer
        var csv = string.Join(",", ids);

        var result = await Ex3(Db, ids);

        Assert.Equal(await Reference.Rows<PersonContact>($"""
            SELECT c.PersonID AS PersonId, c.JobTitle, c.BusinessEntityType
            FROM Person.Person p
            CROSS APPLY dbo.ufnGetContactInformation(p.BusinessEntityID) c
            WHERE p.BusinessEntityID IN (SELECT CAST(value AS int) FROM STRING_SPLIT({csv}, ','))
            ORDER BY c.PersonID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("CROSS APPLY", Sql.All);
    }

    [Exercise]
    public async Task Ex4_MostStocked()
    {
        var result = await Ex4(Db, 10);

        Assert.Equal(await Reference.Rows<ProductStock>($"""
            SELECT TOP (10) ProductID AS ProductId, Name, dbo.ufnGetStock(ProductID) AS Stock
            FROM Production.Product
            ORDER BY Stock DESC, ProductID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("[dbo].[ufnGetStock]", Sql.All);
        Assert.Contains("TOP(", Sql.All); // the top-N cut happens in SQL
    }

    [Exercise]
    public async Task Ex5_ManagementChain()
    {
        var result = await Ex5(Db, 100);

        var expected = (await Reference.Rows<ManagerName>($"EXEC dbo.uspGetEmployeeManagers @BusinessEntityID = {100}"))
            .OrderBy(m => m.RecursionLevel)
            .Select(m => $"{m.ManagerFirstName} {m.ManagerLastName}")
            .ToList();
        Assert.Equal(expected, result);
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ch1_FreshAfterExecuteSql()
    {
        var tracked = await Db.Products.Where(p => p.ProductSubcategoryId == 1).ToListAsync(); // already in the change tracker
        var before = tracked.ToDictionary(p => p.ProductId, p => p.ListPrice);
        Sql.Reset();

        var result = await Ch1(Db, 1, 1.10m);

        var expected = await Reference.Rows<ProductPrice>($"""
            SELECT ProductID AS ProductId, ListPrice FROM Production.Product
            WHERE ProductSubcategoryID = 1
            ORDER BY ProductID
            """);
        Assert.All(expected, e => Assert.NotEqual(before[e.ProductId], e.ListPrice)); // the UPDATE ran
        Assert.Equal(expected, result);                                              // ...and you returned the new prices
        Sql.AssertRoundTrips(2);                                                     // one UPDATE, one SELECT
    }

    [Exercise]
    public async Task Ch2_BillOfMaterials()
    {
        var result = await Ch2(Db, 800, 2);

        Assert.Equal(await Reference.Rows<BomComponent>($"""
            WITH Bom AS (
                SELECT ComponentID, 1 AS Lvl FROM Production.BillOfMaterials
                WHERE ProductAssemblyID = {800} AND EndDate IS NULL
                UNION ALL
                SELECT b.ComponentID, Bom.Lvl + 1 FROM Production.BillOfMaterials b
                JOIN Bom ON b.ProductAssemblyID = Bom.ComponentID
                WHERE b.EndDate IS NULL
            )
            SELECT DISTINCT ComponentID AS ComponentId, Lvl AS Level FROM Bom
            WHERE Lvl >= {2}
            ORDER BY Level, ComponentId
            """), result);
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ch3_SortByUserChoice()
    {
        foreach (var column in new[] { "Name", "ListPrice", "ProductNumber" })
        {
            var expected = column switch
            {
                "Name" => await Reference.Rows<ProductRef>($"SELECT TOP (5) ProductID AS ProductId, Name FROM Production.Product ORDER BY Name, ProductID"),
                "ListPrice" => await Reference.Rows<ProductRef>($"SELECT TOP (5) ProductID AS ProductId, Name FROM Production.Product ORDER BY ListPrice, ProductID"),
                _ => await Reference.Rows<ProductRef>($"SELECT TOP (5) ProductID AS ProductId, Name FROM Production.Product ORDER BY ProductNumber, ProductID"),
            };
            Assert.Equal(expected, await Ch3(Db, column));
        }

        await Assert.ThrowsAnyAsync<ArgumentException>(() => Ch3(Db, "Name; DROP TABLE Production.Product --"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Ch3(Db, "Color")); // a real column, but not on the list
        Sql.AssertRoundTrips(3); // rejected choices never reach the database
    }
}
