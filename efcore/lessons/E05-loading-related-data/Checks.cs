using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E05;

public record OrderSummary(int SalesOrderId, string AccountNumber, int LineCount, decimal TotalDue);
public record TerritoryCustomers(string Territory, int Customers);
public record OrderRef(int CustomerId, int SalesOrderId);

// The checks for E05. E05Exercises (yours) and E05Solutions both inherit them.
// Every check runs on E05Context, where lazy loading is ON: a navigation you forgot to load
// is not silently empty, it costs a round trip, and the round-trip assertions catch it.
public abstract class E05Checks : LabTest<E05Context>
{
    protected abstract Task<List<SalesOrderHeader>> Ex1(E05Context db, int customerId);
    protected abstract Task<Customer> Ex2(E05Context db, int customerId, DateTime since);
    protected abstract Task Ex3(E05Context db, Product product);
    protected abstract Task<List<OrderSummary>> Ex4(E05Context db, DateTime orderDate);
    protected abstract Task<List<TerritoryCustomers>> Ex5(E05Context db);
    protected abstract Task<SalesPerson> Ch1(E05Context db, int salesPersonId);
    protected abstract Task<List<Customer>> Ch2(E05Context db, int minOrders);
    protected abstract Task<int> Ch3(E05Context db, SalesPerson salesPerson, int year);

    [Exercise]
    public async Task Ex1_OrdersWithLines()
    {
        var orders = await Ex1(Db, 11091);

        Sql.AssertRoundTrips(1); // before touching anything: a lazy load would count too
        Assert.True(orders.All(o => Db.Entry(o).Collection(h => h.SalesOrderDetails).IsLoaded),
            "Every order's SalesOrderDetails must be loaded by your query.");
        Assert.Equal(
            await Reference.Rows<int>($"SELECT SalesOrderID AS Value FROM Sales.SalesOrderHeader WHERE CustomerID = 11091 ORDER BY SalesOrderID"),
            orders.Select(o => o.SalesOrderId).ToList());
        Assert.Equal(
            (await Reference.Rows<int>($"""
                SELECT COUNT(*) AS Value FROM Sales.SalesOrderDetail d
                JOIN Sales.SalesOrderHeader h ON h.SalesOrderID = d.SalesOrderID
                WHERE h.CustomerID = 11091
                """)).Single(),
            orders.Sum(o => o.SalesOrderDetails.Count));
        Sql.AssertRoundTrips(1); // counting the lines ran nothing: they were already loaded
    }

    [Exercise]
    public async Task Ex2_RecentOrdersOnly()
    {
        var since = new DateTime(2014, 1, 1);
        var customer = await Ex2(Db, 11091, since);

        Sql.AssertRoundTrips(1);
        Assert.Equal(11091, customer.CustomerId);
        Assert.True(Db.Entry(customer).Collection(c => c.SalesOrderHeaders).IsLoaded);
        Assert.Equal(
            await Reference.Rows<int>($"SELECT SalesOrderID AS Value FROM Sales.SalesOrderHeader WHERE CustomerID = 11091 AND OrderDate >= {since} ORDER BY SalesOrderID"),
            customer.SalesOrderHeaders.Select(h => h.SalesOrderId).Order().ToList());
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ex3_LoadReviewsLater()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 937);
        Sql.Reset();
        Db.ChangeTracker.LazyLoadingEnabled = false; // this exercise is about loading on purpose

        await Ex3(Db, product);

        Sql.AssertRoundTrips(1);
        Assert.True(Db.Entry(product).Collection(p => p.ProductReviews).IsLoaded);
        Assert.DoesNotContain("[Production].[Product] AS", Sql.All); // the product itself was not re-read
        Assert.Equal([2, 4], product.ProductReviews.Select(r => r.Rating).Order().ToList());
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ex4_SummariesInsteadOfGraphs()
    {
        var result = await Ex4(Db, new DateTime(2012, 2, 29));

        Assert.Equal(await Reference.Rows<OrderSummary>($"""
            SELECT h.SalesOrderID AS SalesOrderId, c.AccountNumber,
                   (SELECT COUNT(*) FROM Sales.SalesOrderDetail d WHERE d.SalesOrderID = h.SalesOrderID) AS LineCount,
                   h.TotalDue
            FROM Sales.SalesOrderHeader h
            JOIN Sales.Customer c ON c.CustomerID = h.CustomerID
            WHERE h.OrderDate = '2012-02-29'
            ORDER BY h.SalesOrderID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Empty(Db.ChangeTracker.Entries());         // a projection, not tracked entities
        Assert.DoesNotContain("[Freight]", Sql.All);       // only the columns the summary needs
        Assert.DoesNotContain("[UnitPrice]", Sql.All);     // lines counted in SQL, not loaded
    }

    [Exercise]
    public async Task Ex5_NoNPlusOne()
    {
        var result = await Ex5(Db);

        Assert.Equal(await Reference.Rows<TerritoryCustomers>($"""
            SELECT t.Name AS Territory, COUNT(c.CustomerID) AS Customers
            FROM Sales.SalesTerritory t
            LEFT JOIN Sales.Customer c ON c.TerritoryID = t.TerritoryID
            GROUP BY t.Name
            ORDER BY t.Name
            """), result);
        Assert.Empty(Db.ChangeTracker.Entries()); // counted in SQL, not by loading customers
        Sql.AssertRoundTrips(1); // one query for all territories, not one per territory
    }

    [Exercise]
    public async Task Ch1_NoCartesianExplosion()
    {
        var salesPerson = await Ch1(Db, 277);

        Assert.InRange(Sql.Count, 2, 4);
        Assert.True(Sql.Commands.All(sql => sql.Split("JOIN").Length <= 2), "No single query may join more than one collection to the sales person."); // one collection per query at most
        Assert.True(Db.Entry(salesPerson).Collection(s => s.SalesOrderHeaders).IsLoaded);
        Assert.True(Db.Entry(salesPerson).Collection(s => s.Stores).IsLoaded);
        Assert.True(Db.Entry(salesPerson).Collection(s => s.SalesPersonQuotaHistories).IsLoaded);
        var sqlCount = Sql.Count;
        Assert.Equal(473, salesPerson.SalesOrderHeaders.Count);
        Assert.Equal(76, salesPerson.Stores.Count);
        Assert.Equal(12, salesPerson.SalesPersonQuotaHistories.Count);
        Assert.Equal(sqlCount, Sql.Count); // nothing was lazy-loaded while counting
    }

    [Exercise]
    public async Task Ch2_ThreeMostRecentEach()
    {
        var customers = await Ch2(Db, 27);

        Sql.AssertRoundTrips(1);
        var expected = await Reference.Rows<OrderRef>($"""
            WITH ranked AS (
                SELECT CustomerID, SalesOrderID,
                       ROW_NUMBER() OVER (PARTITION BY CustomerID ORDER BY OrderDate DESC, SalesOrderID DESC) AS rn
                FROM Sales.SalesOrderHeader
                WHERE CustomerID IN (SELECT CustomerID FROM Sales.SalesOrderHeader GROUP BY CustomerID HAVING COUNT(*) >= 27)
            )
            SELECT CustomerID AS CustomerId, SalesOrderID AS SalesOrderId FROM ranked WHERE rn <= 3
            ORDER BY CustomerID, rn
            """);
        Assert.Equal(expected, customers.SelectMany(c => c.SalesOrderHeaders.Select(h => new OrderRef(c.CustomerId, h.SalesOrderId))).ToList());
        Sql.AssertRoundTrips(1);
    }

    [Exercise]
    public async Task Ch3_PartialLoadAndCount()
    {
        var salesPerson = await Db.SalesPeople.SingleAsync(s => s.BusinessEntityId == 277);
        Sql.Reset();

        var stores = await Ch3(Db, salesPerson, 2014);

        Sql.AssertRoundTrips(2);
        Assert.Equal(76, stores);
        Assert.False(Db.Entry(salesPerson).Collection(s => s.Stores).IsLoaded); // counted, not loaded
        Db.ChangeTracker.LazyLoadingEnabled = false; // a partly loaded collection is "not loaded": a lazy load would fetch all 473
        Assert.Equal(
            await Reference.Rows<int>($"SELECT SalesOrderID AS Value FROM Sales.SalesOrderHeader WHERE SalesPersonID = 277 AND YEAR(OrderDate) = 2014 ORDER BY SalesOrderID"),
            salesPerson.SalesOrderHeaders.Select(h => h.SalesOrderId).Order().ToList());
    }
}
