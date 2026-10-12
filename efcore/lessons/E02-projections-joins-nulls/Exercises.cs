using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E02;

// E02 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E02Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
public class E02Exercises : E02Checks
{
    // Exercise 1 — Orders with names
    // For the orders placed on `orderDate` (OrderDate has no time part), return OrderWithNames:
    // SalesOrderId, the customer's person LastName, and the territory Name; ordered by SalesOrderId.
    // Rules: one round trip; navigations only, no whole entities loaded; nothing tracked.
    protected override Task<List<OrderWithNames>> Ex1(AdventureWorksContext db, DateTime orderDate)
        => throw new NotImplementedException();

    // Exercise 2 — A join without a navigation
    // Return every sales person from the VSalesPeople view (Sales.vSalesPerson) with the
    // CountryRegionCode of their territory, matching VSalesPerson.TerritoryName to SalesTerritory.Name.
    // Sales people without a territory must still appear, with a null code. Order by BusinessEntityId.
    // Rules: one round trip; the SQL uses a LEFT JOIN.
    protected override Task<List<SalesPersonCountry>> Ex2(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Exercise 3 — Sold but never purchased
    // Return the distinct ProductIds that appear in SalesOrderDetails but never in
    // PurchaseOrderDetails (AdventureWorks manufactures them), ascending.
    // Rules: one round trip; the SQL uses EXCEPT.
    protected override Task<List<int>> Ex3(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Exercise 4 — "Not red", two ways
    // Count the products that are not red twice: CSharpSemantics, where a NULL Color counts as
    // "not red", and SqlSemantics, where NULL colors are excluded, as in hand-written T-SQL.
    // Rules: at most two round trips; both counts computed by SQL Server.
    protected override Task<NotRedCounts> Ex4(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Exercise 5 — Case-insensitive, for free
    // Return the BusinessEntityIds of people whose LastName equals `lastName` ignoring case
    // (the check passes "SMITH"), ascending.
    // Rules: one round trip; no UPPER() or LOWER() in the SQL.
    protected override Task<List<int>> Ex5(AdventureWorksContext db, string lastName)
        => throw new NotImplementedException();

    // Challenge 1 — Who's on the order?
    // For the orders placed on `orderDate`, return OrderParty ordered by SalesOrderId, where:
    //   Customer    = the store's Name when the customer has a store, else "LastName, FirstName"
    //                 of the customer's person;
    //   SalesPerson = "FirstName LastName" of the order's sales person, or null for online orders
    //                 (they have none).
    // Rules: one round trip; let SQL produce both strings.
    protected override Task<List<OrderParty>> Ch1(AdventureWorksContext db, DateTime orderDate)
        => throw new NotImplementedException();

    // Challenge 2 — One list, two sources
    // Return all vendors (Kind = "Vendor") and all stores (Kind = "Store") whose Name contains `text`,
    // ordered by Name, then Kind. One row per vendor or store: two stores that share a name are
    // two rows.
    // Rules: one round trip.
    protected override Task<List<NamedParty>> Ch2(AdventureWorksContext db, string text)
        => throw new NotImplementedException();

    // Challenge 3 — Good reviews, all products
    // Return every product with each of its reviews rated at least `minRating`, ordered by
    // ProductId, then Rating. A product with no such review appears once with a null Rating —
    // all 504 products must be in the result.
    // Rules: one round trip.
    protected override Task<List<ProductRating>> Ch3(AdventureWorksContext db, int minRating)
        => throw new NotImplementedException();
}
