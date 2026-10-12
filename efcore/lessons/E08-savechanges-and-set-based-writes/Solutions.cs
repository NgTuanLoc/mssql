using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.E08;

// Worked solutions for E08, each followed by a Why note. Run: dotnet test --filter E08Solutions
public class E08Solutions : E08Checks
{
    // Exercise 1 — Insert in one round trip
    // Insert all of `categories` (new ProductCategory objects with only Name set).
    // Rules: one round trip; afterwards each object carries its database-generated ProductCategoryId.
    protected override Task Ex1(AdventureWorksContext db, List<ProductCategory> categories)
    {
        db.ProductCategories.AddRange(categories);
        return db.SaveChangesAsync();
    }
    // Why: SaveChanges batches every pending change into as few commands as it can. Several inserts
    // into the same table become a single MERGE ... OUTPUT INSERTED.[ProductCategoryID], which returns
    // the identity values EF copies back into the objects. Tempting-but-wrong: SaveChanges inside the
    // loop, one round trip per row.

    // Exercise 2 — Raise prices in one statement
    // Raise the ListPrice of every product in subcategory `subcategoryId` by `percent` percent, and
    // return the number of rows updated.
    // Rules: one round trip, an UPDATE; load nothing.
    protected override Task<int> Ex2(AdventureWorksContext db, int subcategoryId, decimal percent)
    {
        var factor = 1 + percent / 100;
        return db.Products
            .Where(p => p.ProductSubcategoryId == subcategoryId)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, p => p.ListPrice * factor));
    }
    // Why: ExecuteUpdate translates the query and the setters into one UPDATE ... FROM ... WHERE, run
    // immediately, without loading or tracking anything. Load-modify-save would read 32 rows and send
    // 32 updates (batched, but still one statement per row).

    // Exercise 3 — Delete in one statement
    // Delete every SalesOrderHeaderSalesReason row of order `salesOrderId`, and return how many
    // rows were deleted.
    // Rules: one round trip, a DELETE.
    protected override Task<int> Ex3(AdventureWorksContext db, int salesOrderId)
        => db.SalesOrderHeaderSalesReasons
            .Where(r => r.SalesOrderId == salesOrderId)
            .ExecuteDeleteAsync();
    // Why: ExecuteDelete is a DELETE with the query's WHERE. No entity has to be loaded first, which
    // Remove() would require.

    // Exercise 4 — No stale entities
    // The products of subcategory `subcategoryId` are already tracked by `db`. Raise their ListPrice
    // by 10% with ExecuteUpdate, then return the subcategory's products showing the new prices.
    // Rules: two round trips (the UPDATE and one query).
    protected override async Task<List<Product>> Ex4(AdventureWorksContext db, int subcategoryId)
    {
        await db.Products
            .Where(p => p.ProductSubcategoryId == subcategoryId)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, p => p.ListPrice * 1.1m));
        db.ChangeTracker.Clear();
        return await db.Products.Where(p => p.ProductSubcategoryId == subcategoryId).ToListAsync();
    }
    // Why: ExecuteUpdate bypasses the change tracker, so the tracked entities keep their old prices.
    // A plain re-query doesn't fix that: identity resolution returns the tracked instances unchanged.
    // Clear() detaches everything, so the next query materializes fresh entities. (Reloading each entity
    // also works, at one round trip per entity.)

    // Exercise 5 — Stamp ModifiedDate
    // Return a SaveChangesInterceptor that, just before saving, sets ModifiedDate to DateTime.Now on
    // every Added or Modified entity that has a ModifiedDate property.
    // Rules: leave Unchanged entities alone; it must work for SaveChangesAsync.
    protected override SaveChangesInterceptor Ex5() => new ModifiedDateStamp();

    sealed class ModifiedDateStamp : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Stamp(eventData.Context!);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Stamp(eventData.Context!);
            return ValueTask.FromResult(result);
        }

        static void Stamp(DbContext db)
        {
            var now = DateTime.Now;
            foreach (var entry in db.ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
                if (entry.Metadata.FindProperty("ModifiedDate") is null) continue;
                entry.Property("ModifiedDate").CurrentValue = now;
            }
        }
    }
    // Why: SavingChanges runs inside SaveChanges, after DetectChanges, so entry states are final and a
    // value set here is saved with everything else. Async saves call SavingChangesAsync only, so override
    // both. FindProperty keeps it generic: any entity type with a ModifiedDate column gets stamped.

    // Challenge 1 — Let the database cascade
    // Delete order `salesOrderId` together with its lines (and its sales reasons).
    // Rules: one round trip. The database's foreign keys cascade from the order to both child tables.
    protected override Task Ch1(AdventureWorksContext db, int salesOrderId)
        => db.SalesOrderHeaders.Where(h => h.SalesOrderId == salesOrderId).ExecuteDeleteAsync();
    // Why: FK_SalesOrderDetail_SalesOrderHeader and FK_SalesOrderHeaderSalesReason_SalesOrderHeader are
    // ON DELETE CASCADE, so deleting the header deletes the children inside the same statement. Tempting-
    // but-wrong: load the order with Include(lines) and Remove it. EF then deletes every tracked line
    // itself (client-side cascade), one DELETE per line, after a round trip to load them.

    // Challenge 2 — Delete a parent of optional children
    // Delete subcategory `subcategoryId`. Its products must keep existing, with no subcategory.
    // Rules: at most two round trips. The database's foreign key is NO ACTION: it won't null the
    // products' ProductSubcategoryID for you.
    protected override async Task Ch2(AdventureWorksContext db, int subcategoryId)
    {
        await db.Products
            .Where(p => p.ProductSubcategoryId == subcategoryId)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ProductSubcategoryId, (int?)null));
        await db.ProductSubcategories.Where(s => s.ProductSubcategoryId == subcategoryId).ExecuteDeleteAsync();
    }
    // Why: the scaffolded relationship is DeleteBehavior.ClientSetNull: EF nulls the FK of dependents
    // it tracks, and the database does nothing. Loading the subcategory with Include(products) and
    // removing it also works (EF sends 32 UPDATEs and the DELETE in one batch: 2 round trips). Removing
    // it without its products fails: the database rejects the DELETE because products still refer to it.

    // Challenge 3 — Set-based writes skip interceptors
    // Raise the ListPrice of every product in subcategory `subcategoryId` by `percent` percent and set
    // their ModifiedDate to `now`, and return the number of rows updated.
    // Rules: one round trip.
    protected override Task<int> Ch3(AdventureWorksContext db, int subcategoryId, decimal percent, DateTime now)
    {
        var factor = 1 + percent / 100;
        return db.Products
            .Where(p => p.ProductSubcategoryId == subcategoryId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(p => p.ListPrice, p => p.ListPrice * factor)
                .SetProperty(p => p.ModifiedDate, now));
    }
    // Why: ExecuteUpdate and ExecuteDelete don't go through SaveChanges, so SaveChanges interceptors
    // (like exercise 5's stamp), the change tracker and its concurrency checks never see them. Whatever
    // the interceptor would have done, the statement has to do itself: one more SetProperty.
}
