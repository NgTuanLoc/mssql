using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E06;

// Worked solutions for E06, each followed by a Why note. Run: dotnet test --filter E06Solutions
public class E06Solutions : E06Checks
{
    // Exercise 1 — Read-only entities
    // Return the Product entities with ListPrice > minPrice, ordered by ProductId, for display only.
    // Rules: one round trip; the change tracker must stay empty.
    protected override Task<List<Product>> Ex1(AdventureWorksContext db, decimal minPrice)
        => db.Products
            .AsNoTracking()
            .Where(p => p.ListPrice > minPrice)
            .OrderBy(p => p.ProductId)
            .ToListAsync();
    // Why: a tracked query stores each entity plus a snapshot of its original values so SaveChanges can
    // find changes later. For read-only results that is pure overhead in memory and CPU. AsNoTracking
    // skips it. The SQL is identical: tracking is a client-side cost.

    // Exercise 2 — One customer instance
    // Return the orders of customer `customerId` with each order's Customer loaded, untracked. All
    // the orders must share a single Customer object, not one copy per order.
    // Rules: one round trip; nothing tracked.
    protected override Task<List<SalesOrderHeader>> Ex2(AdventureWorksContext db, int customerId)
        => db.SalesOrderHeaders
            .AsNoTrackingWithIdentityResolution()
            .Include(h => h.Customer)
            .Where(h => h.CustomerId == customerId)
            .ToListAsync();
    // Why: AsNoTracking materializes a new object for every row, so the customer joined to 28 orders
    // becomes 28 identical Customer objects. AsNoTrackingWithIdentityResolution keeps a lookup for the
    // duration of the query only, so equal keys map to one instance, and still tracks nothing afterwards.

    // Exercise 3 — Keyset paging
    // Return the next `pageSize` orders after SalesOrderId `afterSalesOrderId`, in SalesOrderId order,
    // as OrderRef rows. The first page is requested with afterSalesOrderId = 0.
    // Rules: one round trip per page; no OFFSET.
    protected override Task<List<OrderRef>> Ex3(AdventureWorksContext db, int afterSalesOrderId, int pageSize)
        => db.SalesOrderHeaders
            .Where(h => h.SalesOrderId > afterSalesOrderId)
            .OrderBy(h => h.SalesOrderId)
            .Take(pageSize)
            .Select(h => new OrderRef(h.SalesOrderId, h.OrderDate, h.TotalDue))
            .ToListAsync();
    // Why: OFFSET n makes the server read and discard n rows on every page, so deep pages get slower and
    // rows shift if data changes between requests. "Key greater than the last one seen" is a seek on the
    // clustered index: page 1,000 costs the same as page 1. The price is no "jump to page N".

    // Exercise 4 — A year the index can use
    // Return a query (don't run it) for the currency rates whose CurrencyRateDate falls in calendar
    // year `year`. The check counts it and inspects its plan.
    // Rules: the SQL must not apply a function to CurrencyRateDate, so its index can be used.
    protected override IQueryable<CurrencyRate> Ex4(AdventureWorksContext db, int year)
    {
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);
        return db.CurrencyRates.Where(r => r.CurrencyRateDate >= start && r.CurrencyRateDate < end);
    }
    // Why: r.CurrencyRateDate.Year == year translates to DATEPART(year, [CurrencyRateDate]) = @year,
    // which has to be computed for every row: an index scan. A half-open range on the bare column lets
    // the optimizer seek the AK_CurrencyRate_CurrencyRateDate_... index (T-SQL lesson 12, SARGability).

    // Exercise 5 — Compile it once
    // Return a compiled query that gives a product's Name by ProductId, or null when there is no such
    // product. Every call must return the same delegate.
    // Rules: build it with EF.CompileAsyncQuery; one round trip per execution.
    static readonly Func<AdventureWorksContext, int, Task<string?>> ProductName =
        EF.CompileAsyncQuery((AdventureWorksContext db, int id) =>
            db.Products.Where(p => p.ProductId == id).Select(p => p.Name).SingleOrDefault());

    protected override Func<AdventureWorksContext, int, Task<string?>> Ex5() => ProductName;
    // Why: EF caches compiled queries anyway, but every execution still builds the expression tree and
    // looks it up in the cache. EF.CompileAsyncQuery does that work once and returns a delegate. Keep it
    // in a static field: compiling it on every call throws the saving away.

    // Challenge 1 — Find it in the DMVs
    // Run db.Products.Count(p => p.ListPrice > 0) three times, tagged with `tag` (TagWith). Then read
    // from sys.dm_exec_query_stats (joined to sys.dm_exec_sql_text) how many times SQL Server has executed
    // the statement whose text contains the tag, and return that number.
    // Rules: four round trips: the three counts and one DMV query.
    protected override async Task<long> Ch1(AdventureWorksContext db, string tag)
    {
        for (var i = 0; i < 3; i++)
            await db.Products.TagWith(tag).CountAsync(p => p.ListPrice > 0);

        var pattern = $"%{tag}%";
        return await db.Database.SqlQuery<long>($"""
            SELECT SUM(qs.execution_count) AS Value
            FROM sys.dm_exec_query_stats AS qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
            WHERE st.text LIKE {pattern}
            """).SingleAsync();
    }
    // Why: TagWith puts a "-- tag" comment at the top of the SQL, and the comment is part of the
    // statement text SQL Server stores with the plan. Searching the DMVs (or Query Store, where it is
    // enabled) for the tag finds your LINQ query's server-side statistics. Pass the pattern as a
    // parameter: pasted into the SQL text, the tag would make the DMV query match itself on its next run
    // (and EF1002 would warn about building SQL by interpolation).

    // Challenge 2 — Keyset on two columns
    // Return the next `pageSize` products with ListPrice > 0 after `after` (null = first page), ordered
    // by ListPrice descending, then ProductId, as ProductPrice rows.
    // Rules: one round trip per page; no OFFSET.
    protected override Task<List<ProductPrice>> Ch2(AdventureWorksContext db, ProductPrice? after, int pageSize)
    {
        var query = db.Products.Where(p => p.ListPrice > 0);
        if (after != null)
            query = query.Where(p => p.ListPrice < after.ListPrice
                || (p.ListPrice == after.ListPrice && p.ProductId > after.ProductId));
        return query
            .OrderByDescending(p => p.ListPrice).ThenBy(p => p.ProductId)
            .Take(pageSize)
            .Select(p => new ProductPrice(p.ProductId, p.Name, p.ListPrice))
            .ToListAsync();
    }
    // Why: the keyset predicate has to match the sort exactly: "lower price, or the same price and a
    // higher id". Comparing only ListPrice skips or repeats products that share a price across a page
    // boundary. Composing the Where only when there is a previous page keeps the first page simple.

    // Challenge 3 — A prefix that seeks
    // Return a query (don't run it) for the people whose LastName starts with `prefix`, ignoring case
    // ("sm" and "SM" both match "Smith").
    // Rules: no UPPER/LOWER in the SQL; the IX_Person_LastName_FirstName_MiddleName index must be usable.
    protected override IQueryable<Person> Ch3(AdventureWorksContext db, string prefix)
        => db.People.Where(p => p.LastName.StartsWith(prefix));
    // Why: AdventureWorks uses a case-insensitive collation, so LIKE 'sm%' already matches "Smith".
    // ToUpper()/ToLower() on the column add UPPER(...) around it, and a function on the column means a
    // scan. StartsWith becomes LIKE @prefix ESCAPE, which SQL Server can still turn into an index range seek.
}
