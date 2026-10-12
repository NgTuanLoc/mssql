using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E05;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E05Examples --logger "console;verbosity=detailed"
// Db is an E05Context: lazy loading is on unless an example switches it off.
public class E05Examples : LabTest<E05Context>
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: with lazy loading, the first read of an unloaded navigation runs a query.
    [Fact]
    public async Task Lazy_loading_queries_on_first_touch()
    {
        var customer = await Db.Customers.SingleAsync(c => c.CustomerId == 11091);
        Print(customer.GetType().Name); // CustomerProxy: a generated subclass that intercepts navigations
        Sql.AssertRoundTrips(1);

        var orders = customer.SalesOrderHeaders.Count; // this line runs SQL
        var again = customer.SalesOrderHeaders.Count;  // loaded now: no more SQL

        Assert.Equal(28, orders);
        Assert.Equal(orders, again);
        Sql.AssertRoundTrips(2);
    }

    // Concept 1: without lazy loading, an unloaded navigation is just empty. No error, no query.
    [Fact]
    public async Task Without_lazy_loading_unloaded_navigations_are_empty()
    {
        Db.ChangeTracker.LazyLoadingEnabled = false;
        var customer = await Db.Customers.SingleAsync(c => c.CustomerId == 11091);

        Assert.Empty(customer.SalesOrderHeaders); // 28 orders exist; none were loaded
        Assert.False(Db.Entry(customer).Collection(c => c.SalesOrderHeaders).IsLoaded);
        Sql.AssertRoundTrips(1);
    }

    // Concept 2: a loop over lazy navigations is N+1 round trips. Include makes it one.
    [Fact]
    public async Task N_plus_one_and_its_fix()
    {
        var salesPeople = await Db.SalesPeople.ToListAsync();
        var lazyTotal = salesPeople.Sum(s => s.Stores.Count); // one query per sales person
        Assert.Equal(1 + salesPeople.Count, Sql.Count);       // 1 + 17

        Sql.Reset();
        var eager = await NewContext().SalesPeople.Include(s => s.Stores).ToListAsync();
        Assert.Equal(lazyTotal, eager.Sum(s => s.Stores.Count));
        Sql.AssertRoundTrips(1);
    }

    // Concept 3: Include / ThenInclude add LEFT JOINs to the same query.
    [Fact]
    public async Task Include_and_ThenInclude_join_in_one_query()
    {
        var order = await Db.SalesOrderHeaders
            .Include(h => h.SalesOrderDetails).ThenInclude(d => d.SpecialOfferProduct).ThenInclude(s => s.Product)
            .SingleAsync(h => h.SalesOrderId == 43659);

        Print(string.Join(", ", order.SalesOrderDetails.Select(d => d.SpecialOfferProduct.Product.Name)));
        Sql.AssertRoundTrips(1);
        Assert.Contains("[Production].[Product]", Sql.All);
    }

    // Concept 3: a filtered Include filters, sorts and limits the loaded collection inside the query.
    [Fact]
    public async Task Filtered_Include_with_Take_uses_ROW_NUMBER()
    {
        var customer = await Db.Customers
            .Include(c => c.SalesOrderHeaders.OrderByDescending(h => h.TotalDue).Take(2))
            .SingleAsync(c => c.CustomerId == 11091);

        Assert.Equal(2, customer.SalesOrderHeaders.Count);
        Assert.True(Db.Entry(customer).Collection(c => c.SalesOrderHeaders).IsLoaded);
        Assert.Contains("ROW_NUMBER() OVER(PARTITION BY [s].[CustomerID] ORDER BY [s].[TotalDue] DESC)", Sql.All);
        Sql.AssertRoundTrips(1);
    }

    // Concept 4: explicit loading. Load one navigation when you decide to, or query it without loading.
    [Fact]
    public async Task Explicit_loading_and_Query()
    {
        Db.ChangeTracker.LazyLoadingEnabled = false;
        var product = await Db.Products.SingleAsync(p => p.ProductId == 937);
        var reviews = Db.Entry(product).Collection(p => p.ProductReviews);

        var count = await reviews.Query().CountAsync();   // SELECT COUNT(*): nothing loaded
        Assert.Equal(2, count);
        Assert.False(reviews.IsLoaded);

        await reviews.LoadAsync();                        // SELECT the reviews: now loaded
        Assert.True(reviews.IsLoaded);
        Assert.Equal(2, product.ProductReviews.Count);
        Sql.AssertRoundTrips(3);
    }

    // Concept 5: a projection reads what it selects. No Include, no tracking, no lazy loading.
    [Fact]
    public async Task Projection_needs_no_Include()
    {
        var rows = await Db.SalesOrderHeaders
            .Where(h => h.CustomerId == 11091)
            .Select(h => new { h.SalesOrderId, Lines = h.SalesOrderDetails.Count(), h.Customer.AccountNumber })
            .ToListAsync();

        Assert.Equal(28, rows.Count);
        Assert.Empty(Db.ChangeTracker.Entries());
        Sql.AssertRoundTrips(1);
    }

    // Concept 6: two collection Includes in one query multiply the rows (a cartesian explosion).
    [Fact]
    public async Task Two_collection_Includes_multiply_rows()
    {
        var single = Db.SalesPeople
            .Where(s => s.BusinessEntityId == 277)
            .Include(s => s.SalesOrderHeaders)
            .Include(s => s.Stores);
        var sql = single.ToQueryString();
        Assert.Equal(2, sql.Split("LEFT JOIN").Length - 1);

        // The rows that single query would return: every order paired with every store.
        var rows = (await Reference.Rows<int>($"""
            SELECT COUNT(*) AS Value
            FROM Sales.SalesPerson s
            LEFT JOIN Sales.SalesOrderHeader h ON h.SalesPersonID = s.BusinessEntityID
            LEFT JOIN Sales.Store st ON st.SalesPersonID = s.BusinessEntityID
            WHERE s.BusinessEntityID = 277
            """)).Single();
        Print($"{rows:N0} rows to load 473 orders and 76 stores");
        Assert.Equal(473 * 76, rows);
    }

    // Concept 6: AsSplitQuery sends one query per collection; the rows add up instead.
    [Fact]
    public async Task AsSplitQuery_sends_one_query_per_collection()
    {
        var salesPerson = await Db.SalesPeople
            .Include(s => s.SalesOrderHeaders)
            .Include(s => s.Stores)
            .AsSplitQuery()
            .SingleAsync(s => s.BusinessEntityId == 277);

        Sql.AssertRoundTrips(3); // the sales person, its orders, its stores
        Assert.DoesNotContain("LEFT JOIN", Sql.All);
        Assert.Equal(473, salesPerson.SalesOrderHeaders.Count);
        Assert.Equal(76, salesPerson.Stores.Count);
        Sql.AssertRoundTrips(3);
    }
}
