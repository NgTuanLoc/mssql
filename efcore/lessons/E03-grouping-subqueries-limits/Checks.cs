using EfCoreLessons.Lab;
using EfCoreLessons.Model;

namespace EfCoreLessons.E03;

public record TerritorySales(string Territory, int Orders, int Customers, decimal Total);
public record CustomerOrderMix(int CustomerId, int Total, int Online, int Offline);
public record ProductRef(int ProductId, string Name);
public record TerritoryTopOrder(string Territory, int SalesOrderId, decimal TotalDue);
public record OrderRunningTotal(int SalesOrderId, DateTime OrderDate, decimal TotalDue, decimal RunningTotal);
public record RollupRow(string? Group, string? Territory, decimal Total);
public record SubcategoryTopPrice(int ProductSubcategoryId, int ProductId, decimal ListPrice);

// The checks for E03. E03Exercises (yours) and E03Solutions both inherit them.
public abstract class E03Checks : LabTest
{
    static readonly DateTime From = new(2013, 1, 1);
    static readonly DateTime To = new(2014, 1, 1);

    protected abstract Task<List<TerritorySales>> Ex1(AdventureWorksContext db, DateTime from, DateTime to);
    protected abstract Task<List<CustomerOrderMix>> Ex2(AdventureWorksContext db, int minOrders);
    protected abstract Task<List<ProductRef>> Ex3(AdventureWorksContext db);
    protected abstract Task<List<ProductRef>> Ex4(AdventureWorksContext db, List<int> productIds);
    protected abstract Task<List<TerritoryTopOrder>> Ex5(AdventureWorksContext db, DateTime from, DateTime to);
    protected abstract Task<List<OrderRunningTotal>> Ch1(AdventureWorksContext db, int customerId);
    protected abstract Task<List<RollupRow>> Ch2(AdventureWorksContext db, DateTime from, DateTime to);
    protected abstract Task<List<SubcategoryTopPrice>> Ch3(AdventureWorksContext db);

    [Exercise]
    public async Task Ex1_TerritorySales()
    {
        var result = await Ex1(Db, From, To);

        Assert.Equal(await Reference.Rows<TerritorySales>($"""
            SELECT t.Name AS Territory, COUNT(*) AS Orders, COUNT(DISTINCT h.CustomerID) AS Customers, SUM(h.TotalDue) AS Total
            FROM Sales.SalesOrderHeader h
            JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
            WHERE h.OrderDate >= '2013-01-01' AND h.OrderDate < '2014-01-01'
            GROUP BY t.Name
            ORDER BY Total DESC
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("GROUP BY", Sql.All);
        Assert.Contains("COUNT(DISTINCT", Sql.All);
    }

    [Exercise]
    public async Task Ex2_LoyalCustomers()
    {
        var result = await Ex2(Db, 25);

        Assert.Equal(await Reference.Rows<CustomerOrderMix>($"""
            SELECT CustomerID AS CustomerId, COUNT(*) AS Total,
                   COUNT(CASE WHEN OnlineOrderFlag = 1 THEN 1 END) AS Online,
                   COUNT(CASE WHEN OnlineOrderFlag = 0 THEN 1 END) AS Offline
            FROM Sales.SalesOrderHeader
            GROUP BY CustomerID
            HAVING COUNT(*) >= 25
            ORDER BY Total DESC, CustomerId
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("HAVING", Sql.All); // groups filtered in SQL, not in memory
    }

    [Exercise]
    public async Task Ex3_NeverOrdered()
    {
        var result = await Ex3(Db);

        Assert.Equal(await Reference.Rows<ProductRef>($"""
            SELECT p.ProductID AS ProductId, p.Name
            FROM Production.Product p
            WHERE p.FinishedGoodsFlag = 1
              AND NOT EXISTS (SELECT 1 FROM Sales.SalesOrderDetail d WHERE d.ProductID = p.ProductID)
            ORDER BY p.ProductID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("EXISTS", Sql.All);
        Assert.DoesNotContain("COUNT", Sql.All);
    }

    [Exercise]
    public async Task Ex4_OneSqlForAnyListSize()
    {
        foreach (var ids in new List<int>[] { [707, 708, 709], [710, 711, 712, 713, 714, 715, 716], [.. Enumerable.Range(717, 12)] })
        {
            var csv = string.Join(",", ids);
            Assert.Equal(await Reference.Rows<ProductRef>($"""
                SELECT ProductID AS ProductId, Name FROM Production.Product
                WHERE ProductID IN (SELECT CAST(value AS int) FROM STRING_SPLIT({csv}, ','))
                ORDER BY ProductID
                """), await Ex4(Db, ids));
        }

        Sql.AssertRoundTrips(3);
        Assert.Single(Sql.Commands.Distinct()); // 3, 7 and 12 IDs: one SQL text, one plan
        Assert.Contains("OPENJSON", Sql.All);
    }

    [Exercise]
    public async Task Ex5_TopThreePerTerritory()
    {
        var result = await Ex5(Db, From, To);

        Assert.Equal(await Reference.Rows<TerritoryTopOrder>($"""
            SELECT Territory, SalesOrderId, TotalDue
            FROM (
                SELECT t.Name AS Territory, h.SalesOrderID AS SalesOrderId, h.TotalDue,
                       ROW_NUMBER() OVER (PARTITION BY h.TerritoryID ORDER BY h.TotalDue DESC, h.SalesOrderID) AS rn
                FROM Sales.SalesOrderHeader h
                JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
                WHERE h.OrderDate >= '2013-01-01' AND h.OrderDate < '2014-01-01'
            ) ranked
            WHERE rn <= 3
            ORDER BY Territory, TotalDue DESC, SalesOrderId
            """), result);
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ch1_RunningTotal()
    {
        var result = await Ch1(Db, 11091);

        Assert.Equal(await Reference.Rows<OrderRunningTotal>($"""
            SELECT SalesOrderID AS SalesOrderId, OrderDate, TotalDue,
                   SUM(TotalDue) OVER (ORDER BY OrderDate, SalesOrderID ROWS UNBOUNDED PRECEDING) AS RunningTotal
            FROM Sales.SalesOrderHeader
            WHERE CustomerID = {11091}
            ORDER BY OrderDate, SalesOrderID
            """), result);
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ch2_RollupInLinq()
    {
        var result = await Ch2(Db, From, To);

        var expected = await Reference.Rows<RollupRow>($"""
            SELECT t.[Group] AS [Group], t.Name AS Territory, SUM(h.TotalDue) AS Total
            FROM Sales.SalesOrderHeader h
            JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
            WHERE h.OrderDate >= '2013-01-01' AND h.OrderDate < '2014-01-01'
            GROUP BY ROLLUP (t.[Group], t.Name)
            """);
        Assert.Equal( // any row order is fine
            expected.OrderBy(r => r.Group).ThenBy(r => r.Territory),
            result.OrderBy(r => r.Group).ThenBy(r => r.Territory));
        Sql.AssertRoundTrips(1);
        Assert.Contains("UNION ALL", Sql.All);
        Assert.DoesNotContain("ROLLUP", Sql.All); // LINQ only: no raw SQL
    }

    [Exercise]
    public async Task Ch3_TopPriceWithTies()
    {
        var result = await Ch3(Db);

        Assert.Equal(await Reference.Rows<SubcategoryTopPrice>($"""
            SELECT ProductSubcategoryID AS ProductSubcategoryId, ProductID AS ProductId, ListPrice
            FROM (
                SELECT ProductSubcategoryID, ProductID, ListPrice,
                       RANK() OVER (PARTITION BY ProductSubcategoryID ORDER BY ListPrice DESC) AS r
                FROM Production.Product
                WHERE ProductSubcategoryID IS NOT NULL
            ) ranked
            WHERE r = 1
            ORDER BY ProductSubcategoryId, ProductId
            """), result);
        Sql.AssertRoundTrips(1);
    }
}
