using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E04;

// E04 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E04Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// `db` is an E04Context: everything in AdventureWorksContext plus GetContactInformation and GetStock.
public class E04Exercises : E04Checks
{
    // Exercise 1 — Sounds like
    // Return the people whose LastName sounds like `lastName` according to SQL Server's SOUNDEX,
    // ordered by LastName, FirstName, then BusinessEntityId. LINQ has no SOUNDEX: write that part in
    // SQL. The check also passes a hostile value ("x' OR 1=1 --"), which must be treated as a name.
    // Rules: one round trip per call; the name is a parameter, never part of the SQL text.
    protected override Task<List<PersonName>> Ex1(E04Context db, string lastName)
        => throw new NotImplementedException();

    // Exercise 2 — Window function in SQL, filter in LINQ
    // Return customer `customerId`'s orders with a running total, as in E03 challenge 1, but let
    // SQL Server compute it: write raw SQL that returns CustomerOrderTotal rows for ALL customers
    // (SUM(...) OVER (PARTITION BY CustomerID ...)), then filter to the one customer and order by
    // OrderDate, SalesOrderId with LINQ composed on top.
    // Rules: one round trip; the customer filter is LINQ, not part of your SQL.
    protected override Task<List<OrderRunningTotal>> Ex2(E04Context db, int customerId)
        => throw new NotImplementedException();

    // Exercise 3 — A table-valued function per row
    // For the people in `personIds`, return their dbo.ufnGetContactInformation rows (mapped as
    // db.GetContactInformation) as PersonContact, ordered by PersonId.
    // Rules: one round trip; the SQL uses CROSS APPLY.
    protected override Task<List<PersonContact>> Ex3(E04Context db, List<int> personIds)
        => throw new NotImplementedException();

    // Exercise 4 — A scalar function in ORDER BY
    // Return the `top` products with the most stock according to dbo.ufnGetStock (mapped as
    // E04Context.GetStock), ties broken by ProductId.
    // Rules: one round trip; both the function call and the top-N cut happen in SQL.
    protected override Task<List<ProductStock>> Ex4(E04Context db, int top)
        => throw new NotImplementedException();

    // Exercise 5 — Up the chain
    // Return "FirstName LastName" of each manager returned by dbo.uspGetEmployeeManagers for
    // employee `employeeId`, nearest manager first (ascending RecursionLevel). ManagerName (Checks.cs)
    // matches the columns you need.
    // Rules: one round trip.
    protected override Task<List<string>> Ex5(E04Context db, int employeeId)
        => throw new NotImplementedException();

    // Challenge 1 — Stale after raw SQL
    // The check has already loaded (and is tracking) the products of `subcategoryId`. Multiply their
    // ListPrice by `factor` with ONE raw SQL command, then return every product in the subcategory with
    // its NEW price, ordered by ProductId.
    // Rules: exactly two round trips: one UPDATE, one SELECT.
    protected override Task<List<ProductPrice>> Ch1(E04Context db, int subcategoryId, decimal factor)
        => throw new NotImplementedException();

    // Challenge 2 — Exploding a bill of materials
    // Return every distinct (ComponentId, Level) needed to build product `productId`, walking
    // Production.BillOfMaterials recursively (current rows only: EndDate IS NULL; the product's direct
    // components are level 1), keeping levels >= `minLevel`, ordered by Level, then ComponentId.
    // Rules: one round trip. (You'll need a recursive CTE. Can you compose LINQ over it?)
    protected override Task<List<BomComponent>> Ch2(E04Context db, int productId, int minLevel)
        => throw new NotImplementedException();

    // Challenge 3 — Sort by the user's choice
    // Return the first 5 products as ProductRef, sorted ascending by the column the user picked
    // (`sortColumn` is "Name", "ListPrice" or "ProductNumber"), ties broken by ProductId. Any other
    // value, including SQL fragments, must throw ArgumentException without touching the database.
    // Rules: one round trip per valid call.
    protected override Task<List<ProductRef>> Ch3(E04Context db, string sortColumn)
        => throw new NotImplementedException();
}
