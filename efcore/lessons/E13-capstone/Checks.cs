using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E13;

public record ProductTotal(string Name, decimal TotalSold);
public record TerritoryRevenue(string Territory, decimal AvgMonthlyRevenue);
public record TopCustomer(string Territory, int CustomerId, decimal LifetimeSpend);
public record TerritoryOrders(string Territory, int Orders2012, int Orders2013, int Orders2014, int TotalOrders);
public record RecentOrder(int SalesPersonId, int? SalesOrderId, DateTime? OrderDate, decimal? TotalDue);
public record VendorGrowth(DateTime OrderMonth, decimal MonthTotal, decimal? GrowthPct);
public record VendorReportRow(DateTime OrderMonth, decimal MonthTotal, decimal? GrowthPct, string ProductName, decimal ProductTotal, decimal ContributionPct);
public record OrderLine(short OrderQty, decimal UnitPrice);
public record OrderRequest(int CustomerId, int AddressId, int ShipMethodId, int ProductId, short LocationId, short Quantity);

// The checks for E13. E13Exercises (yours) and E13Solutions both inherit them. Each runs on its own
// context type: E13ExerciseContext (Exercises.cs) or E13SolutionContext (Solutions.cs).
// The reports (exercises 1-5, challenges 1-2) follow the capstone rules: ONE round trip, and every
// filter, grouping, ordering and top-N cut happens in SQL. The checks compare your rows with the
// T-SQL lesson 18 query and read the SQL you sent.
public abstract class E13Checks<TContext> : LabTest<TContext> where TContext : E13Context
{
    protected abstract Task<List<ProductTotal>> Ex1(TContext db);
    protected abstract Task<List<TerritoryRevenue>> Ex2(TContext db);
    protected abstract Task<List<TopCustomer>> Ex3(TContext db, int top);
    protected abstract Task<List<TerritoryOrders>> Ex4(TContext db);
    protected abstract Task<List<RecentOrder>> Ex5(TContext db, int top);
    protected abstract Task<List<VendorGrowth>> Ch1(TContext db, int vendorId);
    protected abstract Task<List<VendorReportRow>> Ch2(TContext db, int vendorId);
    protected abstract Task<int?> Ch3(TContext db, OrderRequest request);

    // Averages compared to the cent: SQL Server's AVG keeps the scale of its input, and different (correct)
    // queries feed it money or decimal.
    static readonly IEqualityComparer<TerritoryRevenue> Cents = EqualityComparer<TerritoryRevenue>.Create(
        (a, b) => a!.Territory == b!.Territory && Math.Round(a.AvgMonthlyRevenue, 2) == Math.Round(b.AvgMonthlyRevenue, 2));

    const int Vendor = 1580; // Litware, Inc.: purchases in most months, several products per month

    [Exercise]
    public async Task Ex1_ProductsAboveAverage()
    {
        var result = await Ex1(Db);

        Assert.Equal(await Reference.Rows<ProductTotal>($"""
            WITH ProductTotals AS (
                SELECT ProductID, SUM(LineTotal) AS TotalSold FROM Sales.SalesOrderDetail GROUP BY ProductID
            )
            SELECT p.Name, pt.TotalSold
            FROM ProductTotals AS pt JOIN Production.Product AS p ON p.ProductID = pt.ProductID
            WHERE pt.TotalSold > (SELECT AVG(TotalSold) FROM ProductTotals)
            ORDER BY pt.TotalSold DESC, p.Name
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("AVG(", Sql.All); // the average is computed by SQL Server, not in memory
    }

    [Exercise]
    public async Task Ex2_AverageMonthlyRevenue()
    {
        var result = await Ex2(Db);

        Assert.Equal(await Reference.Rows<TerritoryRevenue>($"""
            WITH MonthlyRevenue AS (
                SELECT TerritoryID, YEAR(OrderDate) AS Y, MONTH(OrderDate) AS M, SUM(TotalDue) AS Revenue
                FROM Sales.SalesOrderHeader
                GROUP BY TerritoryID, YEAR(OrderDate), MONTH(OrderDate)
            )
            SELECT st.Name AS Territory, AVG(mr.Revenue) AS AvgMonthlyRevenue
            FROM MonthlyRevenue AS mr JOIN Sales.SalesTerritory AS st ON st.TerritoryID = mr.TerritoryID
            GROUP BY st.Name
            ORDER BY AvgMonthlyRevenue DESC
            """), result, Cents);
        Sql.AssertRoundTrips(1);
        Assert.Contains("AVG(", Sql.All);
    }

    [Exercise]
    public async Task Ex3_TopCustomersPerTerritory()
    {
        var result = await Ex3(Db, 3);

        Assert.Equal(await Reference.Rows<TopCustomer>($"""
            WITH CustomerSpend AS (
                SELECT TerritoryID, CustomerID, SUM(TotalDue) AS LifetimeSpend
                FROM Sales.SalesOrderHeader
                GROUP BY TerritoryID, CustomerID
            ),
            Ranked AS (
                SELECT *, ROW_NUMBER() OVER (PARTITION BY TerritoryID ORDER BY LifetimeSpend DESC, CustomerID) AS rn
                FROM CustomerSpend
            )
            SELECT st.Name AS Territory, r.CustomerID AS CustomerId, r.LifetimeSpend
            FROM Ranked AS r JOIN Sales.SalesTerritory AS st ON st.TerritoryID = r.TerritoryID
            WHERE r.rn <= {3}
            ORDER BY st.Name, r.LifetimeSpend DESC, r.CustomerID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.True(Sql.All.Contains("ROW_NUMBER()") || Sql.All.Contains("APPLY"), "Cut the top N in SQL (E03 concept 5)."); // either shape is fine
    }

    [Exercise]
    public async Task Ex4_OrdersPerYear()
    {
        var result = await Ex4(Db);

        Assert.Equal(await Reference.Rows<TerritoryOrders>($"""
            SELECT st.Name AS Territory,
                   COUNT(CASE WHEN YEAR(soh.OrderDate) = 2012 THEN 1 END) AS Orders2012,
                   COUNT(CASE WHEN YEAR(soh.OrderDate) = 2013 THEN 1 END) AS Orders2013,
                   COUNT(CASE WHEN YEAR(soh.OrderDate) = 2014 THEN 1 END) AS Orders2014,
                   COUNT(*) AS TotalOrders
            FROM Sales.SalesOrderHeader AS soh JOIN Sales.SalesTerritory AS st ON st.TerritoryID = soh.TerritoryID
            GROUP BY st.Name
            ORDER BY Territory
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("CASE", Sql.All); // conditional aggregation, in one pass over the orders
    }

    [Exercise]
    public async Task Ex5_RecentOrdersPerSalesPerson()
    {
        // A salesperson with no orders yet (inside the test's transaction, rolled back afterwards).
        await Db.Database.ExecuteSqlAsync($"INSERT INTO Sales.SalesPerson (BusinessEntityID) VALUES ({1})");
        Sql.Reset();

        var result = await Ex5(Db, 3);

        Assert.Equal(await Reference.Rows<RecentOrder>($"""
            SELECT sp.BusinessEntityID AS SalesPersonId, recent.SalesOrderID AS SalesOrderId, recent.OrderDate, recent.TotalDue
            FROM Sales.SalesPerson AS sp
            OUTER APPLY (
                SELECT TOP ({3}) SalesOrderID, OrderDate, TotalDue
                FROM Sales.SalesOrderHeader
                WHERE SalesPersonID = sp.BusinessEntityID
                ORDER BY OrderDate DESC, SalesOrderID DESC
            ) AS recent
            ORDER BY sp.BusinessEntityID, recent.OrderDate DESC, recent.SalesOrderID DESC
            """), result);
        Assert.Contains(result, r => r.SalesPersonId == 1 && r.SalesOrderId == null); // kept, with no orders
        Sql.AssertRoundTrips(1);
        Assert.True(Sql.All.Contains("ROW_NUMBER()") || Sql.All.Contains("APPLY"), "Cut the top N in SQL (E03 concept 5).");
    }

    [Exercise]
    public async Task Ch1_VendorGrowth()
    {
        var result = await Ch1(Db, Vendor);

        Assert.Equal(await Reference.Rows<VendorGrowth>($"""
            WITH VendorMonthly AS (
                SELECT DATEFROMPARTS(YEAR(poh.OrderDate), MONTH(poh.OrderDate), 1) AS OrderMonth, SUM(pod.LineTotal) AS MonthTotal
                FROM Purchasing.PurchaseOrderHeader AS poh
                JOIN Purchasing.PurchaseOrderDetail AS pod ON pod.PurchaseOrderID = poh.PurchaseOrderID
                WHERE poh.VendorID = {Vendor}
                GROUP BY DATEFROMPARTS(YEAR(poh.OrderDate), MONTH(poh.OrderDate), 1)
            ),
            WithPrevious AS (
                SELECT *, LAG(MonthTotal) OVER (ORDER BY OrderMonth) AS PrevMonthTotal FROM VendorMonthly
            )
            SELECT OrderMonth, MonthTotal,
                   ROUND(100 * (MonthTotal - PrevMonthTotal) / NULLIF(PrevMonthTotal, 0), 1) AS GrowthPct
            FROM WithPrevious
            ORDER BY OrderMonth
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("LAG(", Sql.All);
        Assert.Contains("ROUND(", Sql.All); // the growth is computed by SQL Server too
        Assert.NotNull(Db.Model.FindEntityType(typeof(VendorMonth))); // mapped in your context, as challenge 1 asks
    }

    [Exercise]
    public async Task Ch2_VendorReport()
    {
        var result = await Ch2(Db, Vendor);

        Assert.Equal(await Reference.Rows<VendorReportRow>($"""
            WITH PoLines AS (
                SELECT DATEFROMPARTS(YEAR(poh.OrderDate), MONTH(poh.OrderDate), 1) AS OrderMonth, pod.ProductID, pod.LineTotal
                FROM Purchasing.PurchaseOrderHeader AS poh
                JOIN Purchasing.PurchaseOrderDetail AS pod ON pod.PurchaseOrderID = poh.PurchaseOrderID
                WHERE poh.VendorID = {Vendor}
            ),
            VendorMonthly AS (
                SELECT OrderMonth, SUM(LineTotal) AS MonthTotal FROM PoLines GROUP BY OrderMonth
            ),
            WithGrowth AS (
                SELECT *, LAG(MonthTotal) OVER (ORDER BY OrderMonth) AS PrevMonthTotal FROM VendorMonthly
            )
            SELECT g.OrderMonth, g.MonthTotal,
                   ROUND(100 * (g.MonthTotal - g.PrevMonthTotal) / NULLIF(g.PrevMonthTotal, 0), 1) AS GrowthPct,
                   top2.ProductName, top2.ProductTotal,
                   ROUND(100 * top2.ProductTotal / g.MonthTotal, 1) AS ContributionPct
            FROM WithGrowth AS g
            CROSS APPLY (
                SELECT TOP (2) p.Name AS ProductName, SUM(pl.LineTotal) AS ProductTotal
                FROM PoLines AS pl JOIN Production.Product AS p ON p.ProductID = pl.ProductID
                WHERE pl.OrderMonth = g.OrderMonth
                GROUP BY p.Name
                ORDER BY SUM(pl.LineTotal) DESC, p.Name
            ) AS top2
            ORDER BY g.OrderMonth, top2.ProductTotal DESC, top2.ProductName
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("LAG(", Sql.All);
        Assert.Contains("ROUND(", Sql.All); // growth and contribution computed by SQL Server
        Assert.True(Sql.All.Contains("ROW_NUMBER()") || Sql.All.Contains("APPLY"), "Cut the top N in SQL (E03 concept 5).");
    }

    // Product 709 (Mountain Bike Socks, M) has 180 in stock at location 7. Customer 29825's last order
    // shipped to address 985 by ship method 5.
    static OrderRequest Socks(short quantity) => new(29825, 985, 5, 709, 7, quantity);

    async Task<short> Stock() => (await Reference.Rows<short>($"SELECT Quantity AS Value FROM Production.ProductInventory WHERE ProductID = 709 AND LocationID = 7")).Single();

    async Task<int> OrdersOfCustomer() => (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Sales.SalesOrderHeader WHERE CustomerID = 29825")).Single();

    [Exercise]
    public async Task Ch3_PlaceAnOrder()
    {
        var orders = await OrdersOfCustomer();

        var id = await Ch3(Db, Socks(100));

        Assert.NotNull(id);
        Assert.Equal(80, await Stock());
        Assert.Equal(new OrderLine(100, 9.50m),
            (await Reference.Rows<OrderLine>($"SELECT OrderQty, UnitPrice FROM Sales.SalesOrderDetail WHERE SalesOrderID = {id}")).Single());
        Assert.False(Sql.Commands.Any(c => c.Contains("[ProductInventory]") && !c.TrimStart().StartsWith("UPDATE")),
            "Check and take the stock in one UPDATE: reading it first lets two buyers take the same units.");

        // A second buyer wants 100 more: only 80 left.
        Sql.Reset();
        Assert.Null(await Ch3(NewContext(), Socks(100)));
        Assert.Equal(80, await Stock());
        Assert.DoesNotContain("INSERT", Sql.All); // no order without stock
        Assert.Equal(orders + 1, await OrdersOfCustomer());

        // The stock is taken, then saving the order fails (no ship method 999): nothing may remain.
        await Assert.ThrowsAsync<DbUpdateException>(() => Ch3(NewContext(), Socks(10) with { ShipMethodId = 999 }));
        Assert.Equal(80, await Stock());
        Assert.Equal(orders + 1, await OrdersOfCustomer());
    }
}
