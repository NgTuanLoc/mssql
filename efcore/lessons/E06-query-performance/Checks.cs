using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E06;

public record OrderRef(int SalesOrderId, DateTime OrderDate, decimal TotalDue);
public record ProductPrice(int ProductId, string Name, decimal ListPrice);

// The checks for E06. E06Exercises (yours) and E06Solutions both inherit them.
public abstract class E06Checks : LabTest
{
    protected abstract Task<List<Product>> Ex1(AdventureWorksContext db, decimal minPrice);
    protected abstract Task<List<SalesOrderHeader>> Ex2(AdventureWorksContext db, int customerId);
    protected abstract Task<List<OrderRef>> Ex3(AdventureWorksContext db, int afterSalesOrderId, int pageSize);
    protected abstract IQueryable<CurrencyRate> Ex4(AdventureWorksContext db, int year);
    protected abstract Func<AdventureWorksContext, int, Task<string?>> Ex5();
    protected abstract Task<long> Ch1(AdventureWorksContext db, string tag);
    protected abstract Task<List<ProductPrice>> Ch2(AdventureWorksContext db, ProductPrice? after, int pageSize);
    protected abstract IQueryable<Person> Ch3(AdventureWorksContext db, string prefix);

    [Exercise]
    public async Task Ex1_ReadOnlyEntities()
    {
        var products = await Ex1(Db, 3000m);

        Assert.Equal(
            await Reference.Rows<int>($"SELECT ProductID AS Value FROM Production.Product WHERE ListPrice > 3000 ORDER BY ProductID"),
            products.Select(p => p.ProductId).ToList());
        Assert.Empty(Db.ChangeTracker.Entries()); // nothing tracked
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ex2_OneCustomerInstance()
    {
        var orders = await Ex2(Db, 11091);

        Assert.Equal(28, orders.Count);
        Assert.Empty(Db.ChangeTracker.Entries());
        Assert.All(orders, o => Assert.Equal(11091, o.Customer.CustomerId));
        Assert.Single(orders.Select(o => o.Customer).Distinct(ReferenceEqualityComparer.Instance)); // one shared instance
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ex3_KeysetPaging()
    {
        var pages = new List<List<OrderRef>>();
        var after = 0;
        for (var i = 0; i < 3; i++)
        {
            var page = await Ex3(Db, after, 25);
            pages.Add(page);
            after = page[^1].SalesOrderId;
        }

        Assert.Equal(await Reference.Rows<OrderRef>($"""
            SELECT SalesOrderID AS SalesOrderId, OrderDate, TotalDue
            FROM Sales.SalesOrderHeader
            ORDER BY SalesOrderID
            OFFSET 0 ROWS FETCH NEXT 75 ROWS ONLY
            """), pages.SelectMany(p => p).ToList());
        Sql.AssertRoundTrips(3);
        Assert.DoesNotContain("OFFSET", Sql.All);          // no rows skipped and thrown away
        Assert.Contains("[SalesOrderID] > @", Sql.All);    // continue from the last key seen
    }

    [Exercise]
    public async Task Ex4_SargableYear()
    {
        var query = Ex4(Db, 2013);

        Assert.Equal(
            (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Sales.CurrencyRate WHERE CurrencyRateDate >= '2013-01-01' AND CurrencyRateDate < '2014-01-01'")).Single(),
            await query.CountAsync());
        Assert.DoesNotContain("DATEPART", Sql.All);
        var operators = await QueryPlan.OperatorsAsync(Db, query.Select(r => r.CurrencyRateId));
        Assert.Contains("Index Seek", operators); // the CurrencyRateDate index is usable
    }

    [Exercise]
    public async Task Ex5_CompiledOnce()
    {
        var query = Ex5();

        Assert.Same(query, Ex5()); // compiled once and reused, not compiled per call
        Assert.Contains("Compiled", query.Target?.GetType().Name ?? ""); // built with EF.CompileAsyncQuery
        Assert.Equal("Mountain-200 Black, 38", await query(Db, 782));
        Assert.Equal("Adjustable Race", await query(Db, 1));
        Assert.Null(await query(Db, -1));
        Sql.AssertRoundTrips(3);
        Assert.Single(Sql.Commands.Distinct()); // one parameterized SQL text
    }

    [Exercise]
    public async Task Ch1_FindItInTheDmvs()
    {
        var tag = "E06Ch1-" + Guid.NewGuid().ToString("N"); // unique: no earlier run can match

        var executions = await Ch1(Db, tag);

        Assert.Equal(3, executions);
        Sql.AssertRoundTrips(4);
        Assert.All(Sql.Commands.Take(3), sql => Assert.StartsWith($"-- {tag}", sql));
        Assert.Contains("dm_exec_query_stats", Sql.Commands[3]);
    }

    [Exercise]
    public async Task Ch2_KeysetOnTwoColumns()
    {
        var all = new List<ProductPrice>();
        ProductPrice? after = null;
        while (true)
        {
            var page = await Ch2(Db, after, 50);
            all.AddRange(page);
            if (page.Count < 50) break;
            after = page[^1];
        }

        Assert.Equal(await Reference.Rows<ProductPrice>($"""
            SELECT ProductID AS ProductId, Name, ListPrice
            FROM Production.Product
            WHERE ListPrice > 0
            ORDER BY ListPrice DESC, ProductID
            """), all);
        Sql.AssertRoundTrips(7); // 304 products: six full pages and a short one
        Assert.DoesNotContain("OFFSET", Sql.All);
    }

    [Exercise]
    public async Task Ch3_PrefixThatSeeks()
    {
        var expected = (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Person.Person WHERE LastName LIKE 'sm%'")).Single();

        Assert.Equal(expected, await Ch3(Db, "sm").CountAsync());
        Assert.Equal(expected, await Ch3(Db, "SM").CountAsync());
        Assert.DoesNotContain("UPPER(", Sql.All);
        Assert.DoesNotContain("LOWER(", Sql.All);
        var operators = await QueryPlan.OperatorsAsync(Db, Ch3(Db, "Sm").Select(p => p.BusinessEntityId));
        Assert.Contains("Index Seek", operators); // IX_Person_LastName_FirstName_MiddleName is usable
    }
}
