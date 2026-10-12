using System.Globalization;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E01;

// Worked solutions for E01, each followed by a Why note. Run: dotnet test --filter E01Solutions
public class E01Solutions : E01Checks
{
    // Exercise 1 — Managers
    // Return every employee whose JobTitle contains "Manager" as EmployeeTitle rows,
    // ordered by JobTitle, then BusinessEntityId.
    // Rules: one round trip; the SQL selects only the two columns you return.
    protected override Task<List<EmployeeTitle>> Ex1(AdventureWorksContext db)
        => db.Employees
            .Where(e => e.JobTitle.Contains("Manager"))
            .OrderBy(e => e.JobTitle).ThenBy(e => e.BusinessEntityId)
            .Select(e => new EmployeeTitle(e.BusinessEntityId, e.JobTitle))
            .ToListAsync();
    // Why: projecting into the record before ToListAsync makes EF select just those two columns, and a
    // projection of plain columns (no entity instances) is not tracked. Contains("Manager") on a literal becomes LIKE N'%Manager%'.
    // Tempting-but-wrong: ToListAsync() first, then Select — every Employee column crosses the wire and
    // every entity gets tracked, only for the projection to throw most of it away.

    // Exercise 2 — Deterministic paging
    // Return page `page` (1-based, `pageSize` rows per page) of the products with ListPrice > 0,
    // ordered by ListPrice descending. Many prices tie, so break ties by ProductId.
    // Rules: one round trip; the paging happens in SQL (OFFSET/FETCH), not in memory.
    protected override Task<List<ProductPrice>> Ex2(AdventureWorksContext db, int page, int pageSize)
        => db.Products
            .Where(p => p.ListPrice > 0)
            .OrderByDescending(p => p.ListPrice).ThenBy(p => p.ProductId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ProductPrice(p.ProductId, p.Name, p.ListPrice))
            .ToListAsync();
    // Why: Skip/Take after an OrderBy become OFFSET/FETCH, and EF sends both numbers as parameters, so
    // every page reuses one plan. ThenBy(ProductId) makes the order total. Without it, rows with the same
    // price may swap between pages after a re-plan, with no error (T-SQL lesson 02, challenge 1).

    // Exercise 3 — Is there any?
    // Return whether any product has a ListPrice above `minPrice`.
    // Rules: one round trip per call; the SQL uses EXISTS, never COUNT.
    protected override Task<bool> Ex3(AdventureWorksContext db, decimal minPrice)
        => db.Products.AnyAsync(p => p.ListPrice > minPrice);
    // Why: Any becomes SELECT CASE WHEN EXISTS (...), and SQL Server stops at the first matching row.
    // Tempting-but-wrong: CountAsync(...) > 0 counts every matching row just to compare the total with zero.

    // Exercise 4 — SQL without running it
    // Return the SQL EF would run for "the 10 most recent orders (SalesOrderId, OrderDate, TotalDue)
    // of customer `customerId`, newest first" — without executing the query.
    // Rules: zero round trips; customerId stays a parameter.
    protected override string Ex4(AdventureWorksContext db, int customerId)
        => db.SalesOrderHeaders
            .Where(h => h.CustomerId == customerId)
            .OrderByDescending(h => h.OrderDate)
            .Take(10)
            .Select(h => new { h.SalesOrderId, h.OrderDate, h.TotalDue })
            .ToQueryString();
    // Why: ToQueryString renders the SQL, with a DECLARE per parameter, without opening a connection. It
    // is the quickest way to inspect a query while you write it. customerId is a captured variable, so
    // EF sends it as @customerId instead of inlining it.

    // Exercise 5 — Filter on the server, format on the client
    // Return the products with ListPrice > minPrice as ProductLabel(Name, Price), where Price is the
    // ListPrice formatted as US currency ("$3,578.27"), ordered by ListPrice descending, then Name.
    // Rules: one round trip; the WHERE runs in SQL; the SQL selects only the columns you need.
    // Hint: SQL Server can't apply a .NET "C" format string, but the final Select may run .NET code.
    protected override Task<List<ProductLabel>> Ex5(AdventureWorksContext db, decimal minPrice)
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        return db.Products
            .Where(p => p.ListPrice > minPrice)
            .OrderByDescending(p => p.ListPrice).ThenBy(p => p.Name)
            .Select(p => new ProductLabel(p.Name, p.ListPrice.ToString("C", us)))
            .ToListAsync();
    }
    // Why: EF translates everything it can and runs only the untranslatable part of the final Select in
    // .NET. The WHERE and ORDER BY stay in SQL, and only Name and ListPrice cross the wire.
    // Tempting-but-wrong: AsEnumerable() or ToListAsync() before the Where. It returns the right labels,
    // but pulls all 504 products into memory to filter them there.

    // Challenge 1 — Model archaeology
    // Using only EF's metadata (db.Model), return "Schema.Table" for every entity type whose table
    // has triggers, sorted with StringComparer.Ordinal.
    // Rules: zero round trips. Hint: the scaffolder recorded each trigger with HasTrigger.
    protected override List<string> Ch1(AdventureWorksContext db)
        => db.Model.GetEntityTypes()
            .Where(e => e.GetDeclaredTriggers().Any())
            .Select(e => $"{e.GetSchema()}.{e.GetTableName()}")
            .Order(StringComparer.Ordinal)
            .ToList();
    // Why: db.Model is EF's complete mapping metadata (tables, columns, keys, indexes, triggers), and
    // reading it needs no query. The scaffolder declared the triggers because SaveChanges normally reads
    // generated values back with an OUTPUT clause, which SQL Server rejects on a table with triggers.
    // E08 covers this in depth.

    // Challenge 2 — Exactly one
    // Return the product whose ProductNumber starts with `productNumberPrefix`: null when none does,
    // InvalidOperationException when more than one does. The prefix is literal text: "BK_M68B-38"
    // must not match "BK-M68B-38" ('_' is a LIKE wildcard).
    // Rules: one round trip per call, reading at most two rows.
    protected override Task<ProductRef?> Ch2(AdventureWorksContext db, string productNumberPrefix)
        => db.Products
            .Where(p => p.ProductNumber.StartsWith(productNumberPrefix))
            .Select(p => new ProductRef(p.ProductId, p.Name))
            .SingleOrDefaultAsync();
    // Why: SingleOrDefault reads TOP(2): one row to return, plus a second to prove no other match exists.
    // It throws when the second row is there. StartsWith on a variable escapes the LIKE wildcards in the
    // value (ESCAPE N'\'), so '_' stays literal.
    // Tempting-but-wrong: EF.Functions.Like(p.ProductNumber, prefix + "%") passes '_' through as a
    // wildcard, so "BK_M68B-38" matches. FirstOrDefault reads TOP(1) and never notices duplicates.

    // Challenge 3 — A plan per value
    // Count the orders with the given Status. The column is extremely skewed (all 31,465 orders have
    // Status 5), the classic parameter-sniffing setup from T-SQL lesson 13. Make EF inline the status
    // so each value gets its own SQL text, and therefore its own cached plan.
    // Rules: one round trip per call; no parameters in the SQL.
    protected override Task<int> Ch3(AdventureWorksContext db, byte status)
        => db.SalesOrderHeaders.CountAsync(h => h.Status == EF.Constant(status));
    // Why: EF.Constant inlines the value, so status 1 and status 5 produce different SQL text, and SQL
    // Server compiles and caches a plan for each. That is the "plan per value" answer to parameter
    // sniffing. The cost is one plan-cache entry per distinct value, so reserve it for a handful of
    // skewed values, never for an ID column.
}
