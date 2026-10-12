using System.Data.Common;
using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCoreLessons.E09;

public record PriceAndName(string Name, decimal ListPrice);
public record BlockingInfo(int BlockingSessionId, string WaitType);

// The checks for E09. E09Exercises (yours) and E09Solutions both inherit them.
// Db is an E09Context: Product.ModifiedDate is a concurrency token.
public abstract class E09Checks : LabTest<E09Context>
{
    protected abstract Task<bool> Ex1(E09Context db, Product product, decimal newPrice);
    protected abstract Task Ex2(E09Context db, Product product, decimal newPrice);
    protected abstract Task<bool> Ex3(E09Context db, int categoryId, string newName, byte[] version);
    protected abstract Task<bool> Ex4(E09Context db, Product first, decimal firstPrice, Product second, decimal secondPrice);
    protected abstract Task<bool> Ex5(E09Context session, int productId, decimal newPrice);
    protected abstract Task Ch1(E09Context db, int productId, short delta);
    protected abstract Task Ch2(E09Context db, Product product);
    protected abstract Task<BlockingInfo> Ch3(E09Context db);

    // Someone else changes the product's price, and its ModifiedDate as every writer should.
    async Task TheyChangePrice(int productId, decimal price)
    {
        var theirs = NewContext();
        var product = await theirs.Products.SingleAsync(p => p.ProductId == productId);
        product.ListPrice = price;
        product.ModifiedDate = product.ModifiedDate.AddSeconds(1);
        await theirs.SaveChangesAsync();
    }

    Task<PriceAndName> Row(int productId) => Reference.Rows<PriceAndName>(
        $"SELECT Name, ListPrice FROM Production.Product WHERE ProductID = {productId}").ContinueWith(t => t.Result.Single());

    [Exercise]
    public async Task Ex1_DetectAConflict()
    {
        var mine = await Db.Products.SingleAsync(p => p.ProductId == 1);
        await TheyChangePrice(1, 50m);
        Assert.False(await Ex1(Db, mine, 60m)); // they saved first
        Assert.Equal(50m, (await Row(1)).ListPrice);

        var fresh = NewContext(); // Db still holds the conflicting change to product 1
        var unrivalled = await fresh.Products.SingleAsync(p => p.ProductId == 2);
        var before = DateTime.Now;
        Assert.True(await Ex1(fresh, unrivalled, 70m));
        Assert.Equal(70m, (await Row(2)).ListPrice);
        Assert.True(unrivalled.ModifiedDate >= before.AddSeconds(-1), "Stamp ModifiedDate: it's the token other writers rely on.");
    }

    [Exercise]
    public async Task Ex2_YourChangeWins()
    {
        var mine = await Db.Products.SingleAsync(p => p.ProductId == 1);
        await TheyChangePrice(1, 50m);
        Sql.Reset();

        await Ex2(Db, mine, 60m);

        Assert.Equal(60m, (await Row(1)).ListPrice);
        Assert.InRange(Sql.Count, 2, 4); // the failed save, reading the current row, the save that wins
        Assert.False(Db.ChangeTracker.HasChanges());
    }

    [Exercise]
    public async Task Ex3_RowversionFromAForm()
    {
        await E09Context.AddVersionColumnAsync(Db);
        var version = (await Reference.Rows<byte[]>($"SELECT Version AS Value FROM Production.ProductCategory WHERE ProductCategoryID = 1")).Single();
        await NewContext().ProductCategories.Where(c => c.ProductCategoryId == 2)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Name, "Parts")); // a different row: version 1 unaffected
        Sql.Reset();

        Assert.True(await Ex3(Db, 1, "Bicycles", version));
        Sql.AssertRoundTrips(1); // no read before the update
        var stale = version;
        Assert.False(await Ex3(NewContext(), 1, "Cycles", stale)); // the first rename changed the version
        Assert.Equal("Bicycles", (await Reference.Rows<string>($"SELECT Name AS Value FROM Production.ProductCategory WHERE ProductCategoryID = 1")).Single());
    }

    [Exercise]
    public async Task Ex4_KeepTheFirstChange()
    {
        var first = await Db.Products.SingleAsync(p => p.ProductId == 1);
        var second = await Db.Products.SingleAsync(p => p.ProductId == 2);

        var saved = await Ex4(Db, first, 10m, second, -1m); // CK_Product_ListPrice rejects a negative price

        Assert.False(saved);
        Assert.Equal(10m, (await Row(1)).ListPrice);
        Assert.Equal(0m, (await Row(2)).ListPrice);
        Assert.False(Db.ChangeTracker.HasChanges(), "Nothing may be left pending after the failed save.");
        Assert.Equal(504, await Db.Products.CountAsync()); // the transaction is still usable
    }

    [Exercise]
    public async Task Ex5_GiveUpOnALock()
    {
        await Db.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, 1m));
        var session = await NewSessionAsync();

        var started = DateTime.UtcNow;
        Assert.False(await Ex5(session, 1, 5m)); // the test's transaction holds the row
        Assert.True(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(LockTimeoutMs - 500), "It should have waited for the lock first.");

        Assert.True(await Ex5(session, 2, 5m));  // nobody holds this one
        await Assert.ThrowsAnyAsync<Exception>(() => Ex5(session, 2, -1m)); // a CHECK violation is not a lock timeout: it must not be swallowed
    }

    [Exercise]
    public async Task Ch1_NoLostUpdate()
    {
        var original = (await Reference.Rows<short>($"SELECT SafetyStockLevel AS Value FROM Production.Product WHERE ProductID = 1")).Single();
        var db = NewContext<E09Context>(new InterfereOnce(productId: 1, theirDelta: 10));

        await Ch1(db, 1, 5);

        var actual = (await Reference.Rows<short>($"SELECT SafetyStockLevel AS Value FROM Production.Product WHERE ProductID = 1")).Single();
        Assert.True(actual == original + 15, $"Expected {original + 15} (the other user's +10 and your +5), got {actual}. Use SaveChanges with a retry on DbUpdateConcurrencyException; ExecuteUpdate bypasses the interfering save this check simulates.");
    }

    [Exercise]
    public async Task Ch2_MergeBothChanges()
    {
        var mine = await Db.Products.SingleAsync(p => p.ProductId == 1);
        mine.Name = "Adjustable Race (mine)";
        await TheyChangePrice(1, 50m);

        await Ch2(Db, mine);

        Assert.Equal(new PriceAndName("Adjustable Race (mine)", 50m), await Row(1)); // my name, their price
        Assert.False(Db.ChangeTracker.HasChanges());
    }

    [Exercise]
    public async Task Ch3_WhoIsBlocking()
    {
        await Db.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, 1m));
        var mySessionId = (await Reference.Rows<int>($"SELECT CAST(@@SPID AS int) AS Value")).Single();
        var session = await NewSessionAsync();
        var blocked = session.Products.Where(p => p.ProductId == 1).ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, 2m));
        try
        {
            for (var i = 0; i < 20 && (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM sys.dm_exec_requests WHERE blocking_session_id = @@SPID")).Single() == 0; i++)
                await Task.Delay(100); // let it start waiting
            Sql.Reset();

            var info = await Ch3(Db);

            Sql.AssertRoundTrips(1);
            Assert.Equal(mySessionId, info.BlockingSessionId);
            Assert.StartsWith("LCK_M_", info.WaitType);
        }
        finally
        {
            // Always wait for the blocked write to give up (after LockTimeoutMs): a session can't be
            // closed cleanly while one of its commands is still running.
            try { await blocked; }
            catch (Exception) { } // the expected lock timeout; must not mask an earlier failure
        }
    }

    // Plays "another user": the first time db saves, it bumps the product's SafetyStockLevel (and
    // ModifiedDate, as every writer must) directly in the database, just before db's own UPDATE runs.
    sealed class InterfereOnce(int productId, int theirDelta) : SaveChangesInterceptor
    {
        bool done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (done) return result;
            done = true;
            var database = eventData.Context!.Database;
            await using DbCommand command = database.GetDbConnection().CreateCommand();
            command.Transaction = database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = $"""
                UPDATE Production.Product
                SET SafetyStockLevel = SafetyStockLevel + {theirDelta}, ModifiedDate = DATEADD(second, 1, ModifiedDate)
                WHERE ProductID = {productId}
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            return result;
        }
    }
}
