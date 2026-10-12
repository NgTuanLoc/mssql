using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E02;

// Worked solutions for E02, each followed by a Why note. Run: dotnet test --filter E02Solutions
public class E02Solutions : E02Checks
{
    // Exercise 1 — Orders with names
    // For the orders placed on `orderDate` (OrderDate has no time part), return OrderWithNames:
    // SalesOrderId, the customer's person LastName, and the territory Name; ordered by SalesOrderId.
    // Rules: one round trip; navigations only, no whole entities loaded; nothing tracked.
    protected override Task<List<OrderWithNames>> Ex1(AdventureWorksContext db, DateTime orderDate)
        => db.SalesOrderHeaders
            .Where(h => h.OrderDate == orderDate)
            .OrderBy(h => h.SalesOrderId)
            .Select(h => new OrderWithNames(h.SalesOrderId, h.Customer.Person!.LastName, h.Territory!.Name))
            .ToListAsync();
    // Why: each navigation in the projection becomes a join, and EF picks the join type from the model.
    // Customer is required (INNER JOIN), while Customer.Person and Territory are optional (LEFT JOIN).
    // The `!` only silences the compiler; in SQL a missing Person is just NULL.
    // Tempting-but-wrong: Include(h => h.Customer).ThenInclude(c => c.Person) and mapping afterwards. That
    // loads and tracks three full entities per row to read two strings.

    // Exercise 2 — A join without a navigation
    // Return every sales person from the VSalesPeople view (Sales.vSalesPerson) with the
    // CountryRegionCode of their territory, matching VSalesPerson.TerritoryName to SalesTerritory.Name.
    // Sales people without a territory must still appear, with a null code. Order by BusinessEntityId.
    // Rules: one round trip; the SQL uses a LEFT JOIN.
    protected override Task<List<SalesPersonCountry>> Ex2(AdventureWorksContext db)
        => db.VSalesPeople
            .LeftJoin(db.SalesTerritories, v => v.TerritoryName, t => t.Name, (v, t) => new { v, t })
            .OrderBy(x => x.v.BusinessEntityId)
            .Select(x => new SalesPersonCountry(x.v.BusinessEntityId, x.v.LastName, x.t == null ? null : x.t.CountryRegionCode))
            .ToListAsync();
    // Why: the scaffolded views have no navigations, and the match is on names, not a foreign key, so
    // only an explicit join works. EF 10's LeftJoin says "keep every left row" directly. The older
    // GroupJoin + SelectMany(DefaultIfEmpty) pattern produces the same LEFT JOIN. The join yields an
    // anonymous pair and the record comes last, because EF can't order by a positional record's
    // constructor arguments (concept 1).
    // Tempting-but-wrong: Join, which is an INNER JOIN and silently drops the three sales people
    // without a territory.

    // Exercise 3 — Sold but never purchased
    // Return the distinct ProductIds that appear in SalesOrderDetails but never in
    // PurchaseOrderDetails (AdventureWorks manufactures them), ascending.
    // Rules: one round trip; the SQL uses EXCEPT.
    protected override Task<List<int>> Ex3(AdventureWorksContext db)
        => db.SalesOrderDetails.Select(d => d.ProductId)
            .Except(db.PurchaseOrderDetails.Select(d => d.ProductId))
            .OrderBy(id => id)
            .ToListAsync();
    // Why: Except is set difference, and EXCEPT already returns distinct values, so no Distinct() is
    // needed. Ordering after a set operation is fine: EF wraps the EXCEPT in a subquery and orders that.
    // Tempting-but-wrong: loading both ID lists and calling Except in memory. That's two round trips and
    // ships 121,317 + 8,845 rows to compare 190 IDs.

    // Exercise 4 — "Not red", two ways
    // Count the products that are not red twice: CSharpSemantics, where a NULL Color counts as
    // "not red", and SqlSemantics, where NULL colors are excluded, as in hand-written T-SQL.
    // Rules: at most two round trips; both counts computed by SQL Server.
    protected override async Task<NotRedCounts> Ex4(AdventureWorksContext db)
        => new(
            await db.Products.CountAsync(p => p.Color != "Red"),
            await db.Products.CountAsync(p => p.Color != null && p.Color != "Red"));
    // Why: EF gives != C# semantics by adding `OR [Color] IS NULL`, which is why the plain comparison
    // counts the 248 colorless products too (466). To get the T-SQL answer (218), exclude NULL explicitly.
    // This is the mirror image of T-SQL lesson 02, challenge 2: there you had to add `OR Color IS NULL`;
    // with EF you have to take it away.

    // Exercise 5 — Case-insensitive, for free
    // Return the BusinessEntityIds of people whose LastName equals `lastName` ignoring case
    // (the check passes "SMITH"), ascending.
    // Rules: one round trip; no UPPER() or LOWER() in the SQL.
    protected override Task<List<int>> Ex5(AdventureWorksContext db, string lastName)
        => db.People
            .Where(p => p.LastName == lastName)
            .OrderBy(p => p.BusinessEntityId)
            .Select(p => p.BusinessEntityId)
            .ToListAsync();
    // Why: LastName uses the case-insensitive SQL_Latin1_General_CP1_CI_AS collation, so plain equality
    // already ignores case, and it can seek the IX_Person_LastName_FirstName_MiddleName index.
    // Tempting-but-wrong: string.Equals(..., StringComparison.OrdinalIgnoreCase) throws because it can't be
    // translated. p.LastName.ToUpper() == lastName.ToUpper() works but wraps the column in UPPER(), which
    // turns the index seek into a scan.

    // Challenge 1 — Who's on the order?
    // For the orders placed on `orderDate`, return OrderParty ordered by SalesOrderId, where:
    //   Customer    = the store's Name when the customer has a store, else "LastName, FirstName"
    //                 of the customer's person;
    //   SalesPerson = "FirstName LastName" of the order's sales person, or null for online orders
    //                 (they have none).
    // Rules: one round trip; let SQL produce both strings.
    protected override Task<List<OrderParty>> Ch1(AdventureWorksContext db, DateTime orderDate)
        => db.SalesOrderHeaders
            .Where(h => h.OrderDate == orderDate)
            .OrderBy(h => h.SalesOrderId)
            .Select(h => new OrderParty(
                h.SalesOrderId,
                h.Customer.Store != null
                    ? h.Customer.Store.Name
                    : h.Customer.Person!.LastName + ", " + h.Customer.Person.FirstName,
                h.SalesPerson != null
                    ? h.SalesPerson.BusinessEntity.BusinessEntity.FirstName + " " + h.SalesPerson.BusinessEntity.BusinessEntity.LastName
                    : null))
            .ToListAsync();
    // Why: the conditional operator becomes a CASE expression and + becomes string concatenation, so SQL
    // builds both strings. SalesPerson -> Employee -> Person is a chain of navigations after an optional
    // one, and EF keeps the whole chain as LEFT JOINs so online orders survive with a null SalesPerson.
    // Tempting-but-wrong: Include the entities and build the strings in memory, which loads five entity
    // types for two strings.

    // Challenge 2 — One list, two sources
    // Return all vendors (Kind = "Vendor") and all stores (Kind = "Store") whose Name contains `text`,
    // ordered by Name, then Kind. One row per vendor or store: two stores that share a name are
    // two rows.
    // Rules: one round trip.
    protected override Task<List<NamedParty>> Ch2(AdventureWorksContext db, string text)
        => db.Vendors.Where(v => v.Name.Contains(text)).Select(v => new { v.Name, Kind = "Vendor" })
            .Concat(db.Stores.Where(s => s.Name.Contains(text)).Select(s => new { s.Name, Kind = "Store" }))
            .OrderBy(x => x.Name).ThenBy(x => x.Kind)
            .Select(x => new NamedParty(x.Name, x.Kind))
            .ToListAsync();
    // Why: Concat is UNION ALL and keeps every row. Both sides project the same anonymous shape, so the
    // columns line up, and the record is created after the set operation. EF refuses a set operation over
    // a constructor projection ("set operation after client projection").
    // Tempting-but-wrong: Union, which is UNION and removes duplicates, so one of the two "Friendly
    // Bike Shop" stores disappears and the DISTINCT costs a sort you didn't need.

    // Challenge 3 — Good reviews, all products
    // Return every product with each of its reviews rated at least `minRating`, ordered by
    // ProductId, then Rating. A product with no such review appears once with a null Rating —
    // all 504 products must be in the result.
    // Rules: one round trip.
    protected override Task<List<ProductRating>> Ch3(AdventureWorksContext db, int minRating)
        => db.Products
            .LeftJoin(db.ProductReviews.Where(r => r.Rating >= minRating),
                p => p.ProductId, r => r.ProductId,
                (p, r) => new { p.ProductId, Rating = r == null ? (int?)null : r.Rating })
            .OrderBy(x => x.ProductId).ThenBy(x => x.Rating)
            .Select(x => new ProductRating(x.ProductId, x.Rating))
            .ToListAsync();
    // Why: the rating filter belongs to the right-hand side of the join (it's the ON clause in T-SQL), so
    // it filters the reviews before they're joined. Products without a matching review keep their row.
    // Tempting-but-wrong: LeftJoin(db.ProductReviews, ...).Where(x => x.Rating >= minRating). The WHERE
    // runs after the join, NULL >= 4 is never true, and the "left" join quietly becomes an inner join that
    // returns 3 rows instead of 504.
}
