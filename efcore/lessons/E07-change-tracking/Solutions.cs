using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E07;

// Worked solutions for E07, each followed by a Why note. Run: dotnet test --filter E07Solutions
public class E07Solutions : E07Checks
{
    // Exercise 1 — What changed?
    // `product` is tracked by `db` and has been edited in memory. Return its modified properties as
    // PropertyChange(property name, original value, current value), ordered by property name.
    // Rules: read it from the change tracker; no queries.
    protected override List<PropertyChange> Ex1(AdventureWorksContext db, Product product)
        => db.Entry(product).Properties
            .Where(p => p.IsModified)
            .OrderBy(p => p.Metadata.Name)
            .Select(p => new PropertyChange(p.Metadata.Name, p.OriginalValue, p.CurrentValue))
            .ToList();
    // Why: when EF materializes a tracked entity it keeps a snapshot of the values it read. Entry(...)
    // runs DetectChanges for that entity, which compares current values with the snapshot. A property
    // set to the value it already had is not modified, which is why Color doesn't appear.

    // Exercise 2 — Update without loading
    // Set the ListPrice of product `productId` to `listPrice` without reading the product first.
    // Rules: one round trip; the UPDATE writes only ListPrice.
    protected override Task Ex2(AdventureWorksContext db, int productId, decimal listPrice)
    {
        var product = new Product { ProductId = productId };
        db.Products.Attach(product);                     // tracked as Unchanged: EF assumes the row exists
        product.ListPrice = listPrice;                   // DetectChanges will see this one change
        db.Entry(product).Property(p => p.ListPrice).IsModified = true; // even if listPrice happens to be 0
        return db.SaveChangesAsync();
    }
    // Why: Attach starts tracking a stub that has only its key, as Unchanged. Changing one property
    // marks just that property modified, so the UPDATE sets one column. The explicit IsModified
    // covers the case where the new value equals the stub's default (0 here). Tempting-but-wrong:
    // db.Update(new Product { ProductId = ..., ListPrice = ... }) marks every property modified and
    // writes the stub's defaults (a null Name) into the row.

    // Exercise 3 — Apply an edit
    // Load product `edit.ProductId`, copy the edit's values onto it, and save.
    // Rules: two round trips (load, save); the UPDATE writes only the columns whose values changed.
    protected override async Task Ex3(AdventureWorksContext db, ProductEdit edit)
    {
        var product = await db.Products.SingleAsync(p => p.ProductId == edit.ProductId);
        db.Entry(product).CurrentValues.SetValues(edit);
        await db.SaveChangesAsync();
    }
    // Why: CurrentValues.SetValues copies every property whose name matches, from any object (here a
    // record). Values equal to the snapshot stay unmodified, so the UPDATE contains only Name and
    // ListPrice. Loading first is what makes the minimal UPDATE possible: EF needs original values to
    // compare against.

    // Exercise 4 — Anything to save?
    // Return whether calling SaveChanges now would write anything.
    // Rules: no round trips; don't save.
    protected override bool Ex4(AdventureWorksContext db)
        => db.ChangeTracker.HasChanges();
    // Why: HasChanges runs DetectChanges and reports whether any entry is Added, Modified or Deleted.
    // Tempting-but-wrong: ChangeTracker.Entries().Any() is true as soon as anything is tracked, even if
    // nothing changed.

    // Exercise 5 — Discard local changes
    // `product` is tracked by `db` and has unsaved edits. Throw them away: the entity must be
    // Unchanged again and hold the database's current values.
    // Rules: one round trip.
    protected override Task Ex5(AdventureWorksContext db, Product product)
        => db.Entry(product).ReloadAsync();
    // Why: Reload reads the row again and overwrites both current and original values, so the entry
    // becomes Unchanged. Tempting-but-wrong: querying the product again. The query returns the instance
    // already tracked and does not overwrite it, so the edits survive.

    // Challenge 1 — A new product in an existing subcategory
    // Save `newProduct` as a product of `detachedSubcategory` (an existing row, loaded by another
    // context and not tracked by `db`).
    // Rules: the subcategory must not be inserted again.
    protected override async Task Ch1(AdventureWorksContext db, Product newProduct, ProductSubcategory detachedSubcategory)
    {
        db.ProductSubcategories.Attach(detachedSubcategory); // existing: Unchanged
        newProduct.ProductSubcategory = detachedSubcategory;
        db.Products.Add(newProduct);                         // only the new product is Added
        await db.SaveChangesAsync();
    }
    // Why: Add marks every untracked entity reachable from the one you add as Added, including the
    // detached subcategory, so SaveChanges tries to INSERT it again (and fails on its identity key).
    // Attach it first, or skip the navigation and set newProduct.ProductSubcategoryId.

    // Challenge 2 — Save a disconnected graph
    // `detachedOrder` and its SalesOrderDetails were loaded by another context and edited offline:
    // the detail `changedDetailId` was modified, and one new detail (SalesOrderDetailId 0) was added.
    // Save exactly that: insert the new detail, update the changed one, write nothing else.
    protected override async Task Ch2(AdventureWorksContext db, SalesOrderHeader detachedOrder, int changedDetailId)
    {
        db.ChangeTracker.TrackGraph(detachedOrder, node =>
            node.Entry.State = node.Entry.IsKeySet ? EntityState.Unchanged : EntityState.Added);
        var changed = detachedOrder.SalesOrderDetails.Single(d => d.SalesOrderDetailId == changedDetailId);
        db.Entry(changed).State = EntityState.Modified;
        await db.SaveChangesAsync();
    }
    // Why: TrackGraph walks the graph and lets you decide each entity's state. Entities whose generated
    // key is set already exist (Unchanged), the ones without a key are new (Added), and you mark the one
    // you know changed. Tempting-but-wrong: db.Update(detachedOrder) marks the header and every detail
    // Modified and rewrites all of them; db.Add tries to insert the existing rows.

    // Challenge 3 — Merge into the tracked instance
    // `db` already tracks product `staleCopy.ProductId` with unsaved edits. `staleCopy` is a different
    // instance of the same product, carrying a new ListPrice. Copy that ListPrice onto the product `db`
    // tracks, keeping its other edits.
    // Rules: no round trips; don't save.
    protected override async Task Ch3(AdventureWorksContext db, Product staleCopy)
    {
        var tracked = await db.Products.FindAsync(staleCopy.ProductId);
        tracked!.ListPrice = staleCopy.ListPrice;
    }
    // Why: a context tracks at most one instance per key (identity resolution), so Attach or Update with
    // staleCopy throws "another instance with the same key value is already being tracked". FindAsync
    // looks in the tracker first and only queries when the key isn't tracked, so here it costs nothing.
}
