using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E06;

// E06 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E06Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E06Exercises : E06Checks
{
    // Exercise 1 — Read-only entities
    // Return the Product entities with ListPrice > minPrice, ordered by ProductId, for display only.
    // Rules: one round trip; the change tracker must stay empty.
    protected override Task<List<Product>> Ex1(AdventureWorksContext db, decimal minPrice)
        => throw new NotImplementedException();

    // Exercise 2 — One customer instance
    // Return the orders of customer `customerId` with each order's Customer loaded, untracked. All
    // the orders must share a single Customer object, not one copy per order.
    // Rules: one round trip; nothing tracked.
    protected override Task<List<SalesOrderHeader>> Ex2(AdventureWorksContext db, int customerId)
        => throw new NotImplementedException();

    // Exercise 3 — Keyset paging
    // Return the next `pageSize` orders after SalesOrderId `afterSalesOrderId`, in SalesOrderId order,
    // as OrderRef rows. The first page is requested with afterSalesOrderId = 0.
    // Rules: one round trip per page; no OFFSET.
    protected override Task<List<OrderRef>> Ex3(AdventureWorksContext db, int afterSalesOrderId, int pageSize)
        => throw new NotImplementedException();

    // Exercise 4 — A year the index can use
    // Return a query (don't run it) for the currency rates whose CurrencyRateDate falls in calendar
    // year `year`. The check counts it and inspects its plan.
    // Rules: the SQL must not apply a function to CurrencyRateDate, so its index can be used.
    protected override IQueryable<CurrencyRate> Ex4(AdventureWorksContext db, int year)
        => throw new NotImplementedException();

    // Exercise 5 — Compile it once
    // Return a compiled query that gives a product's Name by ProductId, or null when there is no such
    // product. Every call must return the same delegate.
    // Rules: build it with EF.CompileAsyncQuery; one round trip per execution.
    protected override Func<AdventureWorksContext, int, Task<string?>> Ex5()
        => throw new NotImplementedException();

    // Challenge 1 — Find it in the DMVs
    // Run db.Products.Count(p => p.ListPrice > 0) three times, tagged with `tag` (TagWith). Then read
    // from sys.dm_exec_query_stats (joined to sys.dm_exec_sql_text) how many times SQL Server has executed
    // the statement whose text contains the tag, and return that number.
    // Rules: four round trips: the three counts and one DMV query.
    protected override Task<long> Ch1(AdventureWorksContext db, string tag)
        => throw new NotImplementedException();

    // Challenge 2 — Keyset on two columns
    // Return the next `pageSize` products with ListPrice > 0 after `after` (null = first page), ordered
    // by ListPrice descending, then ProductId, as ProductPrice rows.
    // Rules: one round trip per page; no OFFSET.
    protected override Task<List<ProductPrice>> Ch2(AdventureWorksContext db, ProductPrice? after, int pageSize)
        => throw new NotImplementedException();

    // Challenge 3 — A prefix that seeks
    // Return a query (don't run it) for the people whose LastName starts with `prefix`, ignoring case
    // ("sm" and "SM" both match "Smith").
    // Rules: no UPPER/LOWER in the SQL; the IX_Person_LastName_FirstName_MiddleName index must be usable.
    protected override IQueryable<Person> Ch3(AdventureWorksContext db, string prefix)
        => throw new NotImplementedException();
}
