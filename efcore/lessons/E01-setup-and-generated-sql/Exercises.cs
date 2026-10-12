using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E01;

// E01 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E01Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E01Exercises : E01Checks
{
    // Exercise 1 — Managers
    // Return every employee whose JobTitle contains "Manager" as EmployeeTitle rows,
    // ordered by JobTitle, then BusinessEntityId.
    // Rules: one round trip; the SQL selects only the two columns you return.
    protected override Task<List<EmployeeTitle>> Ex1(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Exercise 2 — Deterministic paging
    // Return page `page` (1-based, `pageSize` rows per page) of the products with ListPrice > 0,
    // ordered by ListPrice descending. Many prices tie, so break ties by ProductId.
    // Rules: one round trip; the paging happens in SQL (OFFSET/FETCH), not in memory.
    protected override Task<List<ProductPrice>> Ex2(AdventureWorksContext db, int page, int pageSize)
        => throw new NotImplementedException();

    // Exercise 3 — Is there any?
    // Return whether any product has a ListPrice above `minPrice`.
    // Rules: one round trip per call; the SQL uses EXISTS, never COUNT.
    protected override Task<bool> Ex3(AdventureWorksContext db, decimal minPrice)
        => throw new NotImplementedException();

    // Exercise 4 — SQL without running it
    // Return the SQL EF would run for "the 10 most recent orders (SalesOrderId, OrderDate, TotalDue)
    // of customer `customerId`, newest first" — without executing the query.
    // Rules: zero round trips; customerId stays a parameter.
    protected override string Ex4(AdventureWorksContext db, int customerId)
        => throw new NotImplementedException();

    // Exercise 5 — Filter on the server, format on the client
    // Return the products with ListPrice > minPrice as ProductLabel(Name, Price), where Price is the
    // ListPrice formatted as US currency ("$3,578.27"), ordered by ListPrice descending, then Name.
    // Rules: one round trip; the WHERE runs in SQL; the SQL selects only the columns you need.
    // Hint: SQL Server can't apply a .NET "C" format string, but the final Select may run .NET code.
    protected override Task<List<ProductLabel>> Ex5(AdventureWorksContext db, decimal minPrice)
        => throw new NotImplementedException();

    // Challenge 1 — Model archaeology
    // Using only EF's metadata (db.Model), return "Schema.Table" for every entity type whose table
    // has triggers, sorted with StringComparer.Ordinal.
    // Rules: zero round trips. Hint: the scaffolder recorded each trigger with HasTrigger.
    protected override List<string> Ch1(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Challenge 2 — Exactly one
    // Return the product whose ProductNumber starts with `productNumberPrefix`: null when none does,
    // InvalidOperationException when more than one does. The prefix is literal text: "BK_M68B-38"
    // must not match "BK-M68B-38" ('_' is a LIKE wildcard).
    // Rules: one round trip per call, reading at most two rows.
    protected override Task<ProductRef?> Ch2(AdventureWorksContext db, string productNumberPrefix)
        => throw new NotImplementedException();

    // Challenge 3 — A plan per value
    // Count the orders with the given Status. The column is extremely skewed (all 31,465 orders have
    // Status 5), the classic parameter-sniffing setup from T-SQL lesson 13. Make EF inline the status
    // so each value gets its own SQL text, and therefore its own cached plan.
    // Rules: one round trip per call; no parameters in the SQL.
    protected override Task<int> Ch3(AdventureWorksContext db, byte status)
        => throw new NotImplementedException();
}
