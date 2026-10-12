using System.Data;
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E09;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E09Examples --logger "console;verbosity=detailed"
// Db is an E09Context: Product.ModifiedDate is a concurrency token.
public class E09Examples : LabTest<E09Context>
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: a concurrency token goes into the WHERE clause; 0 rows updated means a conflict.
    [Fact]
    public async Task ModifiedDate_as_a_concurrency_token()
    {
        var mine = await Db.Products.SingleAsync(p => p.ProductId == 1);
        var theirContext = NewContext();
        var theirs = await theirContext.Products.SingleAsync(p => p.ProductId == 1);
        theirs.ListPrice = 50m;
        theirs.ModifiedDate = DateTime.Now;
        await theirContext.SaveChangesAsync();

        mine.ListPrice = 60m;
        mine.ModifiedDate = DateTime.Now;
        var conflict = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Db.SaveChangesAsync());

        Print(conflict.Message);
        Assert.Contains("[ModifiedDate] = @", Sql.Commands[^1]); // WHERE [ProductID] = @p AND [ModifiedDate] = @p
        Assert.Same(mine, conflict.Entries.Single().Entity);
    }

    // Concept 1: a rowversion is compared on UPDATE and refreshed from OUTPUT INSERTED.
    [Fact]
    public async Task Rowversion_is_checked_and_refreshed()
    {
        await E09Context.AddVersionColumnAsync(Db);
        var category = await Db.ProductCategories.SingleAsync(c => c.ProductCategoryId == 1);
        var before = (byte[])Db.Entry(category).Property("Version").CurrentValue!;

        category.Name = "Bicycles";
        await Db.SaveChangesAsync();

        var after = (byte[])Db.Entry(category).Property("Version").CurrentValue!;
        Assert.Contains("[Version] = @", Sql.Commands[^1]);
        Assert.Contains("OUTPUT INSERTED.[Version]", Sql.Commands[^1]);
        Assert.NotEqual(before, after); // the database changed it; EF read it back
    }

    // Concept 2: resolving a conflict — store wins (reload) or client wins (refresh the originals).
    [Fact]
    public async Task Store_wins_or_client_wins()
    {
        var storeWins = await Db.Products.SingleAsync(p => p.ProductId == 1);
        var other = NewContext();
        var clientWins = await other.Products.SingleAsync(p => p.ProductId == 1);
        await Db.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set
            .SetProperty(p => p.ListPrice, 50m)
            .SetProperty(p => p.ModifiedDate, p => p.ModifiedDate.AddSeconds(1)));

        storeWins.ListPrice = 60m;
        var conflict = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Db.SaveChangesAsync());
        await conflict.Entries.Single().ReloadAsync();                    // theirs: 50, my edit discarded
        Assert.Equal(50m, storeWins.ListPrice);

        clientWins.ListPrice = 70m;
        conflict = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
        var entry = conflict.Entries.Single();
        entry.OriginalValues.SetValues((await entry.GetDatabaseValuesAsync())!); // pretend I loaded theirs
        await other.SaveChangesAsync();                                           // mine: 70
        Assert.Equal(70m, (await Reference.Rows<decimal>($"SELECT ListPrice AS Value FROM Production.Product WHERE ProductID = 1")).Single());
    }

    // Concept 3: inside an open transaction SaveChanges uses a savepoint: a failed batch leaves nothing behind.
    [Fact]
    public async Task A_failed_SaveChanges_rolls_back_its_whole_batch()
    {
        var first = await Db.Products.SingleAsync(p => p.ProductId == 1);
        var second = await Db.Products.SingleAsync(p => p.ProductId == 2);
        first.ListPrice = 10m;  // fine on its own
        second.ListPrice = -1m; // CK_Product_ListPrice: ListPrice >= 0

        await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());

        var prices = await Reference.Rows<decimal>($"SELECT ListPrice AS Value FROM Production.Product WHERE ProductID IN (1, 2) ORDER BY ProductID");
        Assert.Equal([0m, 0m], prices);                       // the first UPDATE was undone too
        Assert.Equal(504, await Db.Products.CountAsync());    // and the transaction is still usable
    }

    // Concept 3: savepoints you create yourself on the current transaction.
    [Fact]
    public async Task Savepoints_you_create()
    {
        var transaction = Db.Database.CurrentTransaction!; // the test's transaction, as UseTransaction set it
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);

        await transaction.CreateSavepointAsync("BeforeRename");
        product.Name = "Renamed";
        await Db.SaveChangesAsync();
        await transaction.RollbackToSavepointAsync("BeforeRename");

        Assert.Equal("Adjustable Race", (await Reference.Rows<string>($"SELECT Name AS Value FROM Production.Product WHERE ProductID = 1")).Single());
        Assert.Equal("Renamed", product.Name); // the entity doesn't know: reload it or clear the tracker
    }

    // Concept 3: SQL Server has no nested transactions, and EF won't start a second one.
    [Fact]
    public async Task BeginTransaction_inside_a_transaction_throws()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Db.Database.BeginTransactionAsync());

        Assert.Contains("already in a transaction", failure.Message);
    }

    // Concept 4: with READ_COMMITTED_SNAPSHOT, readers see the last committed version; READ UNCOMMITTED sees dirty data.
    [Fact]
    public async Task Readers_and_isolation_levels()
    {
        await Db.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set.SetProperty(p => p.Name, "Uncommitted"));

        var readCommitted = await NewSessionAsync();
        var readUncommitted = await NewSessionAsync(IsolationLevel.ReadUncommitted);

        Assert.Equal("Adjustable Race", await NameOf1(readCommitted));  // no waiting: the row version store
        Assert.Equal("Uncommitted", await NameOf1(readUncommitted));    // a dirty read
    }

    // Concept 5: writers block writers until the lock timeout, then error 1222, wrapped by EF.
    [Fact]
    public async Task Writers_block_writers_until_the_lock_timeout()
    {
        await Db.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set.SetProperty(p => p.Name, "Locked"));
        var session = await NewSessionAsync();

        var failure = await Assert.ThrowsAnyAsync<Exception>(() =>
            session.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set.SetProperty(p => p.Name, "Mine")));

        Print(failure.Message);
        Assert.IsType<InvalidOperationException>(failure);                    // "likely due to a transient failure"
        Assert.Equal(1222, Assert.IsType<SqlException>(failure.InnerException).Number);
    }

    // Concept 6: a retrying execution strategy refuses to run inside a transaction it didn't start.
    [Fact]
    public async Task Retrying_strategy_rejects_user_transactions()
    {
        var retrying = NewContext<RetryingContext>(); // EnableRetryOnFailure, on the test's transaction

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => retrying.Products.CountAsync());

        Print(failure.Message);
        Assert.Contains("does not support user-initiated transactions", failure.Message);
    }

    static Task<string> NameOf1(E09Context session)
        => session.Products.Where(p => p.ProductId == 1).Select(p => p.Name).SingleAsync();
}
