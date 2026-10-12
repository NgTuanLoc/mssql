using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E07;

// E07 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E07Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E07Exercises : E07Checks
{
    // Exercise 1 — What changed?
    // `product` is tracked by `db` and has been edited in memory. Return its modified properties as
    // PropertyChange(property name, original value, current value), ordered by property name.
    // Rules: read it from the change tracker; no queries.
    protected override List<PropertyChange> Ex1(AdventureWorksContext db, Product product)
        => throw new NotImplementedException();

    // Exercise 2 — Update without loading
    // Set the ListPrice of product `productId` to `listPrice` without reading the product first.
    // Rules: one round trip; the UPDATE writes only ListPrice.
    protected override Task Ex2(AdventureWorksContext db, int productId, decimal listPrice)
        => throw new NotImplementedException();

    // Exercise 3 — Apply an edit
    // Load product `edit.ProductId`, copy the edit's values onto it, and save.
    // Rules: two round trips (load, save); the UPDATE writes only the columns whose values changed.
    protected override Task Ex3(AdventureWorksContext db, ProductEdit edit)
        => throw new NotImplementedException();

    // Exercise 4 — Anything to save?
    // Return whether calling SaveChanges now would write anything.
    // Rules: no round trips; don't save.
    protected override bool Ex4(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Exercise 5 — Discard local changes
    // `product` is tracked by `db` and has unsaved edits. Throw them away: the entity must be
    // Unchanged again and hold the database's current values.
    // Rules: one round trip.
    protected override Task Ex5(AdventureWorksContext db, Product product)
        => throw new NotImplementedException();

    // Challenge 1 — A new product in an existing subcategory
    // Save `newProduct` as a product of `detachedSubcategory` (an existing row, loaded by another
    // context and not tracked by `db`).
    // Rules: the subcategory must not be inserted again.
    protected override Task Ch1(AdventureWorksContext db, Product newProduct, ProductSubcategory detachedSubcategory)
        => throw new NotImplementedException();

    // Challenge 2 — Save a disconnected graph
    // `detachedOrder` and its SalesOrderDetails were loaded by another context and edited offline:
    // the detail `changedDetailId` was modified, and one new detail (SalesOrderDetailId 0) was added.
    // Save exactly that: insert the new detail, update the changed one, write nothing else.
    protected override Task Ch2(AdventureWorksContext db, SalesOrderHeader detachedOrder, int changedDetailId)
        => throw new NotImplementedException();

    // Challenge 3 — Merge into the tracked instance
    // `db` already tracks product `staleCopy.ProductId` with unsaved edits. `staleCopy` is a different
    // instance of the same product, carrying a new ListPrice. Copy that ListPrice onto the product `db`
    // tracks, keeping its other edits.
    // Rules: no round trips; don't save.
    protected override Task Ch3(AdventureWorksContext db, Product staleCopy)
        => throw new NotImplementedException();
}
