using EfCoreLessons.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E09;

// Worked solutions for E09, each followed by a Why note. Run: dotnet test --filter E09Solutions
public class E09Solutions : E09Checks
{
    // Exercise 1 — Detect a conflict
    // `product` is tracked by `db`. Set its ListPrice to `newPrice`, stamp ModifiedDate with
    // DateTime.Now, and save. Return true if saved, false if someone else changed the product since
    // it was loaded (ModifiedDate is the concurrency token in E09Context).
    protected override async Task<bool> Ex1(E09Context db, Product product, decimal newPrice)
    {
        product.ListPrice = newPrice;
        product.ModifiedDate = DateTime.Now;
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
    // Why: a concurrency token's original value goes into the UPDATE's WHERE clause. If another writer
    // changed it, the UPDATE matches 0 rows, and EF turns "expected 1 row, got 0" into
    // DbUpdateConcurrencyException. ModifiedDate only works as a token if every writer updates it, which
    // is why you stamp it too.

    // Exercise 2 — Your change wins
    // Like exercise 1, but on a conflict your price must win: overwrite whatever the other writer saved.
    // Rules: at most 4 round trips; nothing pending afterwards.
    protected override async Task Ex2(E09Context db, Product product, decimal newPrice)
    {
        product.ListPrice = newPrice;
        product.ModifiedDate = DateTime.Now;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException conflict)
        {
            foreach (var entry in conflict.Entries)
                entry.OriginalValues.SetValues((await entry.GetDatabaseValuesAsync())!);
            await db.SaveChangesAsync();
        }
    }
    // Why: "client wins" means: pretend you loaded the row after the other writer saved it. Copying the
    // database values into OriginalValues refreshes the token, so the retry's WHERE matches. Current
    // values stay yours, and properties that differ from the new originals are written. Store wins would
    // be entry.ReloadAsync() instead.

    // Exercise 3 — Rowversion from a form
    // A web form rendered category `categoryId` with its rowversion `version`. Rename the category to
    // `newName` only if nobody changed it since: return true if saved, false on a conflict.
    // Rules: one round trip (don't read the category first). The rowversion is the shadow property
    // "Version" (see E09Context).
    protected override async Task<bool> Ex3(E09Context db, int categoryId, string newName, byte[] version)
    {
        var category = new ProductCategory { ProductCategoryId = categoryId };
        db.ProductCategories.Attach(category);
        db.Entry(category).Property("Version").OriginalValue = version;
        category.Name = newName;
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
    // Why: with a rowversion the database changes the token on every write, so no writer can forget it.
    // In a disconnected flow the version travels with the form, and you hand it back to EF as the
    // property's original value: that's what goes into WHERE [Version] = @p. (For a rowversion, which EF
    // never writes, setting the current value on the Unchanged stub happens to end up in the WHERE too.
    // Don't rely on that for a token EF does write, such as ModifiedDate: there the current value is the
    // new value to SET, and the WHERE still compares the original.)

    // Exercise 4 — Keep the first change
    // Set `first`'s ListPrice to `firstPrice` and `second`'s to `secondPrice` (both are tracked). The
    // database may reject the second change. If it does, the first must still be saved. Return whether
    // the second change was saved.
    // Rules: nothing may be left pending afterwards.
    protected override async Task<bool> Ex4(E09Context db, Product first, decimal firstPrice, Product second, decimal secondPrice)
    {
        first.ListPrice = firstPrice;
        await db.SaveChangesAsync();

        second.ListPrice = secondPrice;
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            await db.Entry(second).ReloadAsync(); // undo the rejected change locally
            return false;
        }
    }
    // Why: when a transaction is already open, SaveChanges sets a savepoint first and rolls back to it if
    // any statement fails, so a failed save never leaves half its batch behind. That works against you
    // if both changes go into one SaveChanges: the second's failure undoes the first. Two saves, two
    // savepoints. The failed entity stays Modified in memory, so reload it, or the next save retries it.

    // Exercise 5 — Give up on a lock
    // From `session` (a second, independent session), set product `productId`'s ListPrice to `newPrice`
    // with ExecuteUpdate. If the row is locked by another transaction and the lock timeout expires,
    // return false instead of throwing; otherwise return true.
    // Hint: lock timeouts are SQL Server error 1222. Look at the exception EF actually throws.
    protected override async Task<bool> Ex5(E09Context session, int productId, decimal newPrice)
    {
        try
        {
            await session.Products.Where(p => p.ProductId == productId)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, newPrice));
            return true;
        }
        catch (Exception ex) when ((ex as SqlException ?? ex.InnerException as SqlException)?.Number == 1222)
        {
            return false;
        }
    }
    // Why: the session waits for the lock, then SQL Server cancels the statement with error 1222 (only
    // where SET LOCK_TIMEOUT is set, as here: without it the server waits indefinitely, and the client's command
    // timeout, 30 s by default, cancels first with a timeout error, -2, not 1222). EF classifies 1222 as transient and wraps it
    // in an InvalidOperationException that suggests EnableRetryOnFailure, so `catch (SqlException)` alone
    // misses it. Filter on the number, wherever the SqlException sits.

    // Challenge 1 — No lost update
    // Increase product `productId`'s SafetyStockLevel by `delta` and stamp ModifiedDate. Another user may
    // change the same product between your read and your save. Retry on conflicts (up to 3 attempts)
    // so that neither change is lost.
    protected override async Task Ch1(E09Context db, int productId, short delta)
    {
        var product = await db.Products.SingleAsync(p => p.ProductId == productId);
        for (var attempt = 1; ; attempt++)
        {
            product.SafetyStockLevel += delta;
            product.ModifiedDate = DateTime.Now;
            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 3)
            {
                await db.Entry(product).ReloadAsync(); // their value, then add mine again
            }
        }
    }
    // Why: an increment is a read-modify-write. On a conflict, "client wins" would write your stale
    // total and erase their increment: the classic lost update. Reload, re-apply the change to fresh
    // data, and save again. (For a pure increment, ExecuteUpdate with SafetyStockLevel + delta avoids
    // the race entirely, because the database does the read and the write in one statement.)

    // Challenge 2 — Merge both changes
    // `product` is tracked and has your unsaved edits. Another user has saved changes to other
    // properties in the meantime. Save so that both survive: your value wherever you changed a
    // property, theirs everywhere else.
    // Rules: nothing pending afterwards.
    protected override async Task Ch2(E09Context db, Product product)
    {
        product.ModifiedDate = DateTime.Now;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException conflict)
        {
            foreach (var entry in conflict.Entries)
            {
                var databaseValues = (await entry.GetDatabaseValuesAsync())!;
                foreach (var property in entry.Properties)
                {
                    if (!property.IsModified)
                        property.CurrentValue = databaseValues[property.Metadata];
                    property.OriginalValue = databaseValues[property.Metadata];
                }
            }
            await db.SaveChangesAsync();
        }
    }
    // Why: the conflict exception gives you three versions of each row: original (what you loaded),
    // current (what you want) and the database's (what they saved). A field-level merge keeps your value
    // for the properties you modified and takes theirs for the rest, then refreshes all originals so
    // the token check passes. Client wins would have written your stale ListPrice over theirs.

    // Challenge 3 — Who is blocking?
    // Right now another session is blocked, waiting for a lock that `db`'s transaction holds. Return
    // the blocked request's blocking_session_id and wait_type, read from sys.dm_exec_requests.
    // Rules: one round trip.
    protected override Task<BlockingInfo> Ch3(E09Context db)
        => db.Database.SqlQuery<BlockingInfo>($"""
            SELECT CAST(blocking_session_id AS int) AS BlockingSessionId, wait_type AS WaitType
            FROM sys.dm_exec_requests
            WHERE blocking_session_id = @@SPID
            """).SingleAsync();
    // Why: sys.dm_exec_requests lists the requests running right now. A blocked one shows who blocks it
    // (blocking_session_id) and what it waits for (LCK_M_X / LCK_M_U: an exclusive or update lock). Under
    // READ_COMMITTED_SNAPSHOT, which AdventureWorks has on, readers don't wait for writers, so blocking
    // like this is between writers (T-SQL lesson 17).
}
