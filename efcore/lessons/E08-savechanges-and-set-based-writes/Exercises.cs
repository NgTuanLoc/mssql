using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.E08;

// E08 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E08Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E08Exercises : E08Checks
{
    // Exercise 1 — Insert in one round trip
    // Insert all of `categories` (new ProductCategory objects with only Name set).
    // Rules: one round trip; afterwards each object carries its database-generated ProductCategoryId.
    protected override Task Ex1(AdventureWorksContext db, List<ProductCategory> categories)
        => throw new NotImplementedException();

    // Exercise 2 — Raise prices in one statement
    // Raise the ListPrice of every product in subcategory `subcategoryId` by `percent` percent, and
    // return the number of rows updated.
    // Rules: one round trip, an UPDATE; load nothing.
    protected override Task<int> Ex2(AdventureWorksContext db, int subcategoryId, decimal percent)
        => throw new NotImplementedException();

    // Exercise 3 — Delete in one statement
    // Delete every SalesOrderHeaderSalesReason row of order `salesOrderId`, and return how many
    // rows were deleted.
    // Rules: one round trip, a DELETE.
    protected override Task<int> Ex3(AdventureWorksContext db, int salesOrderId)
        => throw new NotImplementedException();

    // Exercise 4 — No stale entities
    // The products of subcategory `subcategoryId` are already tracked by `db`. Raise their ListPrice
    // by 10% with ExecuteUpdate, then return the subcategory's products showing the new prices.
    // Rules: two round trips (the UPDATE and one query).
    protected override Task<List<Product>> Ex4(AdventureWorksContext db, int subcategoryId)
        => throw new NotImplementedException();

    // Exercise 5 — Stamp ModifiedDate
    // Return a SaveChangesInterceptor that, just before saving, sets ModifiedDate to DateTime.Now on
    // every Added or Modified entity that has a ModifiedDate property.
    // Rules: leave Unchanged entities alone; it must work for SaveChangesAsync.
    protected override SaveChangesInterceptor Ex5()
        => throw new NotImplementedException();

    // Challenge 1 — Let the database cascade
    // Delete order `salesOrderId` together with its lines (and its sales reasons).
    // Rules: one round trip. The database's foreign keys cascade from the order to both child tables.
    protected override Task Ch1(AdventureWorksContext db, int salesOrderId)
        => throw new NotImplementedException();

    // Challenge 2 — Delete a parent of optional children
    // Delete subcategory `subcategoryId`. Its products must keep existing, with no subcategory.
    // Rules: at most two round trips. The database's foreign key is NO ACTION: it won't null the
    // products' ProductSubcategoryID for you.
    protected override Task Ch2(AdventureWorksContext db, int subcategoryId)
        => throw new NotImplementedException();

    // Challenge 3 — Set-based writes skip interceptors
    // Raise the ListPrice of every product in subcategory `subcategoryId` by `percent` percent and set
    // their ModifiedDate to `now`, and return the number of rows updated.
    // Rules: one round trip.
    protected override Task<int> Ch3(AdventureWorksContext db, int subcategoryId, decimal percent, DateTime now)
        => throw new NotImplementedException();
}
