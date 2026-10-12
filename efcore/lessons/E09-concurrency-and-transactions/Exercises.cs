using EfCoreLessons.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E09;

// E09 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E09Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E09Exercises : E09Checks
{
    // Exercise 1 — Detect a conflict
    // `product` is tracked by `db`. Set its ListPrice to `newPrice`, stamp ModifiedDate with
    // DateTime.Now, and save. Return true if saved, false if someone else changed the product since
    // it was loaded (ModifiedDate is the concurrency token in E09Context).
    protected override Task<bool> Ex1(E09Context db, Product product, decimal newPrice)
        => throw new NotImplementedException();

    // Exercise 2 — Your change wins
    // Like exercise 1, but on a conflict your price must win: overwrite whatever the other writer saved.
    // Rules: at most 4 round trips; nothing pending afterwards.
    protected override Task Ex2(E09Context db, Product product, decimal newPrice)
        => throw new NotImplementedException();

    // Exercise 3 — Rowversion from a form
    // A web form rendered category `categoryId` with its rowversion `version`. Rename the category to
    // `newName` only if nobody changed it since: return true if saved, false on a conflict.
    // Rules: one round trip (don't read the category first). The rowversion is the shadow property
    // "Version" (see E09Context).
    protected override Task<bool> Ex3(E09Context db, int categoryId, string newName, byte[] version)
        => throw new NotImplementedException();

    // Exercise 4 — Keep the first change
    // Set `first`'s ListPrice to `firstPrice` and `second`'s to `secondPrice` (both are tracked). The
    // database may reject the second change. If it does, the first must still be saved. Return whether
    // the second change was saved.
    // Rules: nothing may be left pending afterwards.
    protected override Task<bool> Ex4(E09Context db, Product first, decimal firstPrice, Product second, decimal secondPrice)
        => throw new NotImplementedException();

    // Exercise 5 — Give up on a lock
    // From `session` (a second, independent session), set product `productId`'s ListPrice to `newPrice`
    // with ExecuteUpdate. If the row is locked by another transaction and the lock timeout expires,
    // return false instead of throwing; otherwise return true.
    // Hint: lock timeouts are SQL Server error 1222. Look at the exception EF actually throws.
    protected override Task<bool> Ex5(E09Context session, int productId, decimal newPrice)
        => throw new NotImplementedException();

    // Challenge 1 — No lost update
    // Increase product `productId`'s SafetyStockLevel by `delta` and stamp ModifiedDate. Another user may
    // change the same product between your read and your save. Retry on conflicts (up to 3 attempts)
    // so that neither change is lost.
    protected override Task Ch1(E09Context db, int productId, short delta)
        => throw new NotImplementedException();

    // Challenge 2 — Merge both changes
    // `product` is tracked and has your unsaved edits. Another user has saved changes to other
    // properties in the meantime. Save so that both survive: your value wherever you changed a
    // property, theirs everywhere else.
    // Rules: nothing pending afterwards.
    protected override Task Ch2(E09Context db, Product product)
        => throw new NotImplementedException();

    // Challenge 3 — Who is blocking?
    // Right now another session is blocked, waiting for a lock that `db`'s transaction holds. Return
    // the blocked request's blocking_session_id and wait_type, read from sys.dm_exec_requests.
    // Rules: one round trip.
    protected override Task<BlockingInfo> Ch3(E09Context db)
        => throw new NotImplementedException();
}
