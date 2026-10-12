using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E04;

// Worked solutions for E04, each followed by a Why note. Run: dotnet test --filter E04Solutions
public class E04Solutions : E04Checks
{
    // Exercise 1 — Sounds like
    // Return the people whose LastName sounds like `lastName` according to SQL Server's SOUNDEX,
    // ordered by LastName, FirstName, then BusinessEntityId. LINQ has no SOUNDEX: write that part in
    // SQL. The check also passes a hostile value ("x' OR 1=1 --"), which must be treated as a name.
    // Rules: one round trip per call; the name is a parameter, never part of the SQL text.
    protected override Task<List<PersonName>> Ex1(E04Context db, string lastName)
        => db.People
            .FromSql($"SELECT * FROM Person.Person WHERE SOUNDEX(LastName) = SOUNDEX({lastName})")
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.BusinessEntityId)
            .Select(p => new PersonName(p.BusinessEntityId, p.FirstName, p.LastName))
            .ToListAsync();
    // Why: FromSql receives a FormattableString, so {lastName} becomes @p0 and the hostile value is just
    // an odd name that sounds like nothing. Only the part LINQ can't say (SOUNDEX) is SQL. Ordering and
    // projection are LINQ composed on top, which also keeps ORDER BY out of the raw SQL, where it would
    // break the subquery.
    // Tempting-but-wrong: FromSqlRaw("... SOUNDEX('" + lastName + "')"). The hostile input closes the quote
    // and rewrites the query; here it produces a syntax error, and with luckier input it returns other
    // people's rows.

    // Exercise 2 — Window function in SQL, filter in LINQ
    // Return customer `customerId`'s orders with a running total, as in E03 challenge 1, but let
    // SQL Server compute it: write raw SQL that returns CustomerOrderTotal rows for ALL customers
    // (SUM(...) OVER (PARTITION BY CustomerID ...)), then filter to the one customer and order by
    // OrderDate, SalesOrderId with LINQ composed on top.
    // Rules: one round trip; the customer filter is LINQ, not part of your SQL.
    protected override Task<List<OrderRunningTotal>> Ex2(E04Context db, int customerId)
        => db.Database
            .SqlQuery<CustomerOrderTotal>($"""
                SELECT CustomerID AS CustomerId, SalesOrderID AS SalesOrderId, OrderDate, TotalDue,
                       SUM(TotalDue) OVER (PARTITION BY CustomerID ORDER BY OrderDate, SalesOrderID
                                           ROWS UNBOUNDED PRECEDING) AS RunningTotal
                FROM Sales.SalesOrderHeader
                """)
            .Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.OrderDate).ThenBy(x => x.SalesOrderId)
            .Select(x => new OrderRunningTotal(x.SalesOrderId, x.OrderDate, x.TotalDue, x.RunningTotal))
            .ToListAsync();
    // Why: SqlQuery<T> results compose like a table. EF wraps the SQL as a derived table and adds
    // WHERE/ORDER BY outside it. PARTITION BY CustomerID is what makes the outer filter correct: each
    // customer's running total restarts at their first order. Without the partition you'd get a running
    // total across all customers, filtered afterwards. The query is reusable for any customer, and SQL
    // Server can push the CustomerId predicate into the windowed subquery because it's on the partitioning
    // column.

    // Exercise 3 — A table-valued function per row
    // For the people in `personIds`, return their dbo.ufnGetContactInformation rows (mapped as
    // db.GetContactInformation) as PersonContact, ordered by PersonId.
    // Rules: one round trip; the SQL uses CROSS APPLY.
    protected override Task<List<PersonContact>> Ex3(E04Context db, List<int> personIds)
        => db.People
            .Where(p => personIds.Contains(p.BusinessEntityId))
            .SelectMany(p => db.GetContactInformation(p.BusinessEntityId))
            .OrderBy(c => c.PersonId)
            .Select(c => new PersonContact(c.PersonId, c.JobTitle, c.BusinessEntityType))
            .ToListAsync();
    // Why: a mapped TVF is an IQueryable, so SelectMany can call it per outer row, which is exactly what
    // CROSS APPLY means. Everything stays one statement: the filter, the function, the ordering.
    // Tempting-but-wrong: looping over personIds and calling db.GetContactInformation(id) for each, one
    // round trip per person.

    // Exercise 4 — A scalar function in ORDER BY
    // Return the `top` products with the most stock according to dbo.ufnGetStock (mapped as
    // E04Context.GetStock), ties broken by ProductId.
    // Rules: one round trip; both the function call and the top-N cut happen in SQL.
    protected override Task<List<ProductStock>> Ex4(E04Context db, int top)
        => db.Products
            .Select(p => new { p.ProductId, p.Name, Stock = E04Context.GetStock(p.ProductId) })
            .OrderByDescending(x => x.Stock).ThenBy(x => x.ProductId)
            .Take(top)
            .Select(x => new ProductStock(x.ProductId, x.Name, x.Stock))
            .ToListAsync();
    // Why: the static stub is mapped to dbo.ufnGetStock, so EF writes the function call into the SELECT and
    // ORDER BY, and TOP(@p) keeps only `top` rows. The body of GetStock never runs.
    // Tempting-but-wrong: selecting the stock for all 504 products and sorting in memory. One round trip,
    // but it ships 504 rows to keep 10, and it calls the UDF 504 times for nothing.

    // Exercise 5 — Up the chain
    // Return "FirstName LastName" of each manager returned by dbo.uspGetEmployeeManagers for
    // employee `employeeId`, nearest manager first (ascending RecursionLevel). ManagerName (Checks.cs)
    // matches the columns you need.
    // Rules: one round trip.
    protected override async Task<List<string>> Ex5(E04Context db, int employeeId)
    {
        var chain = await db.Database
            .SqlQuery<ManagerName>($"EXEC dbo.uspGetEmployeeManagers @BusinessEntityID = {employeeId}")
            .ToListAsync();

        return chain
            .OrderBy(m => m.RecursionLevel)
            .Select(m => $"{m.ManagerFirstName} {m.ManagerLastName}")
            .ToList();
    }
    // Why: SqlQuery maps the procedure's columns onto ManagerName by name and ignores the ones it doesn't
    // need. EXEC can't be wrapped in a subquery, so ordering happens after materializing, in memory. That's
    // fine for a three-row management chain.
    // Tempting-but-wrong: .OrderBy(m => m.RecursionLevel) before ToListAsync throws "non-composable SQL".

    // Challenge 1 — Stale after raw SQL
    // The check has already loaded (and is tracking) the products of `subcategoryId`. Multiply their
    // ListPrice by `factor` with ONE raw SQL command, then return every product in the subcategory with
    // its NEW price, ordered by ProductId.
    // Rules: exactly two round trips: one UPDATE, one SELECT.
    protected override async Task<List<ProductPrice>> Ch1(E04Context db, int subcategoryId, decimal factor)
    {
        await db.Database.ExecuteSqlAsync(
            $"UPDATE Production.Product SET ListPrice = ListPrice * {factor} WHERE ProductSubcategoryID = {subcategoryId}");

        return await db.Products
            .Where(p => p.ProductSubcategoryId == subcategoryId)
            .OrderBy(p => p.ProductId)
            .Select(p => new ProductPrice(p.ProductId, p.ListPrice))
            .ToListAsync();
    }
    // Why: ExecuteSql goes straight to the database, so the tracked Product instances still hold the old
    // prices. A projection reads column values from the result set and never consults the change tracker,
    // so it sees the new prices. (AsNoTracking() on an entity query works too.)
    // Tempting-but-wrong: db.Products.Where(...).ToListAsync() and mapping afterwards. Identity resolution
    // returns the already-tracked instances, unchanged, with every price stale. Calling Entry(p).Reload()
    // per product fixes it at the cost of one round trip each. E08 shows ExecuteUpdate, the LINQ way to
    // write the same UPDATE.

    // Challenge 2 — Exploding a bill of materials
    // Return every distinct (ComponentId, Level) needed to build product `productId`, walking
    // Production.BillOfMaterials recursively (current rows only: EndDate IS NULL; the product's direct
    // components are level 1), keeping levels >= `minLevel`, ordered by Level, then ComponentId.
    // Rules: one round trip. (You'll need a recursive CTE. Can you compose LINQ over it?)
    protected override Task<List<BomComponent>> Ch2(E04Context db, int productId, int minLevel)
        => db.Database
            .SqlQuery<BomComponent>($"""
                WITH Bom AS (
                    SELECT ComponentID, 1 AS Lvl FROM Production.BillOfMaterials
                    WHERE ProductAssemblyID = {productId} AND EndDate IS NULL
                    UNION ALL
                    SELECT b.ComponentID, Bom.Lvl + 1 FROM Production.BillOfMaterials b
                    JOIN Bom ON b.ProductAssemblyID = Bom.ComponentID
                    WHERE b.EndDate IS NULL
                )
                SELECT DISTINCT ComponentID AS ComponentId, Lvl AS Level FROM Bom
                WHERE Lvl >= {minLevel}
                ORDER BY Level, ComponentId
                """)
            .ToListAsync();
    // Why: no, you can't compose over it. A CTE can't sit inside a derived table, so EF refuses to compose
    // over SQL that starts with WITH ("non-composable SQL"). The filter, DISTINCT and ORDER BY therefore
    // live inside the SQL, parameterized like any other value, and the query runs exactly as written.
    // Materializing everything and filtering in memory also works, but ships the levels you don't want.

    // Challenge 3 — Sort by the user's choice
    // Return the first 5 products as ProductRef, sorted ascending by the column the user picked
    // (`sortColumn` is "Name", "ListPrice" or "ProductNumber"), ties broken by ProductId. Any other
    // value, including SQL fragments, must throw ArgumentException without touching the database.
    // Rules: one round trip per valid call.
    protected override Task<List<ProductRef>> Ch3(E04Context db, string sortColumn)
    {
        IOrderedQueryable<Product> ordered = sortColumn switch
        {
            "Name" => db.Products.OrderBy(p => p.Name),
            "ListPrice" => db.Products.OrderBy(p => p.ListPrice),
            "ProductNumber" => db.Products.OrderBy(p => p.ProductNumber),
            _ => throw new ArgumentException($"Cannot sort by '{sortColumn}'.", nameof(sortColumn)),
        };
        return ordered.ThenBy(p => p.ProductId)
            .Take(5)
            .Select(p => new ProductRef(p.ProductId, p.Name))
            .ToListAsync();
    }
    // Why: parameters carry values, never identifiers, so `ORDER BY @column` can't work, and concatenating
    // the user's text into ORDER BY is injection. Mapping each allowed choice to a lambda means user input
    // never becomes SQL text, and anything else fails before a connection is touched. If you need raw SQL,
    // the same rule applies: pick from a fixed set of column names, never splice the input.
}
