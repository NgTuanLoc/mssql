using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E08;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E08Examples --logger "console;verbosity=detailed"
public class E08Examples : LabTest
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: one insert is INSERT ... OUTPUT; several into one table become a single MERGE.
    [Fact]
    public async Task Inserts_are_batched()
    {
        Db.ProductCategories.Add(new ProductCategory { Name = "E08 single" });
        await Db.SaveChangesAsync();
        Assert.Contains("INSERT INTO [Production].[ProductCategory]", Sql.Commands[0]);
        Assert.Contains("OUTPUT INSERTED.[ProductCategoryID]", Sql.Commands[0]);

        Db.ProductCategories.AddRange(new ProductCategory { Name = "E08 a" }, new ProductCategory { Name = "E08 b" }, new ProductCategory { Name = "E08 c" });
        Db.SalesReasons.Add(new SalesReason { Name = "E08 reason", ReasonType = "Other" });
        await Db.SaveChangesAsync();

        Sql.AssertRoundTrips(2);  // every change of the second save in one command
        var batch = Sql.Commands[1];
        Assert.Contains("MERGE [Production].[ProductCategory]", batch);
        Assert.Contains("INSERT INTO [Sales].[SalesReason]", batch);
    }

    // Concept 2: on a table with a trigger, EF avoids OUTPUT and reads @@ROWCOUNT instead.
    [Fact]
    public async Task Tables_with_triggers_get_no_OUTPUT_clause()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);          // no trigger
        var order = await Db.SalesOrderHeaders.SingleAsync(h => h.SalesOrderId == 43659); // uSalesOrderHeader
        product.Name = "Changed";
        order.Comment = "Changed";
        Sql.Reset();

        await Db.SaveChangesAsync();
        Print(Sql.All);

        var batch = Sql.Commands.Single();
        var orderUpdate = batch[batch.IndexOf("UPDATE [Sales].[SalesOrderHeader]")..];
        Assert.Matches(@"UPDATE \[Production\]\.\[Product\] SET \[Name\] = @p0\s+OUTPUT 1", batch);
        Assert.DoesNotContain("OUTPUT", orderUpdate);   // a trigger table: no OUTPUT clause
        Assert.Contains("@@ROWCOUNT = 1", orderUpdate); // computed columns read back with a separate SELECT
    }

    // Concept 2: without HasTrigger in the model, SQL Server rejects EF's OUTPUT clause.
    [Fact]
    public async Task Without_HasTrigger_the_update_fails()
    {
        var db = NewContext<E08Context>(); // its model has no trigger declared on SalesOrderHeader
        var order = await db.SalesOrderHeaders.SingleAsync(h => h.SalesOrderId == 43659);
        order.Comment = "Changed";

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Print(failure.InnerException!.Message);
        Assert.Contains("cannot have any enabled triggers if the statement contains an OUTPUT clause", failure.InnerException.Message);
    }

    // Concept 3: ExecuteUpdate / ExecuteDelete are one statement, run now, with nothing tracked.
    [Fact]
    public async Task ExecuteUpdate_and_ExecuteDelete_are_single_statements()
    {
        var updated = await Db.Products
            .Where(p => p.ProductSubcategoryId == 1)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, p => p.ListPrice * 1.1m));
        var deleted = await Db.SalesOrderHeaderSalesReasons
            .Where(r => r.SalesOrderId == 43697)
            .ExecuteDeleteAsync();

        Assert.Equal(32, updated);
        Assert.Equal(2, deleted);
        Sql.AssertRoundTrips(2);
        Assert.Empty(Db.ChangeTracker.Entries());
    }

    // Concept 3: ExecuteUpdate doesn't touch tracked entities, and a re-query doesn't refresh them.
    [Fact]
    public async Task ExecuteUpdate_leaves_tracked_entities_stale()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 771);
        var before = product.ListPrice;

        await Db.Products.Where(p => p.ProductId == 771).ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, 1m));
        var requeried = await Db.Products.SingleAsync(p => p.ProductId == 771);

        Assert.Same(product, requeried);
        Assert.Equal(before, requeried.ListPrice); // stale: the row now says 1.00
        await Db.Entry(product).ReloadAsync();
        Assert.Equal(1m, product.ListPrice);
    }

    // Concept 4: tracked dependents are deleted by EF; untracked ones by the database's ON DELETE CASCADE.
    [Fact]
    public async Task Client_cascade_vs_database_cascade()
    {
        var withLines = await Db.SalesOrderHeaders.Include(h => h.SalesOrderDetails).SingleAsync(h => h.SalesOrderId == 43660);
        Db.Remove(withLines);
        Sql.Reset();
        await Db.SaveChangesAsync();
        Assert.Contains("DELETE FROM [Sales].[SalesOrderDetail]", Sql.All); // EF deleted the 2 tracked lines

        var alone = await Db.SalesOrderHeaders.SingleAsync(h => h.SalesOrderId == 43661);
        Db.Remove(alone);
        Sql.Reset();
        await Db.SaveChangesAsync();
        Assert.DoesNotContain("[Sales].[SalesOrderDetail]", Sql.All);        // the database deleted the lines
        Assert.Equal(0, (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43661")).Single());
    }

    // Concept 4: ClientSetNull nulls the FKs of tracked dependents; untracked ones block the delete.
    [Fact]
    public async Task ClientSetNull_nulls_tracked_dependents()
    {
        var subcategory = await Db.ProductSubcategories.Include(s => s.Products).SingleAsync(s => s.ProductSubcategoryId == 1);
        Db.Remove(subcategory);
        Sql.Reset();

        await Db.SaveChangesAsync();

        Assert.Equal(32, Sql.All.Split("UPDATE [Production].[Product] SET [ProductSubcategoryID] = @").Length - 1);
        Assert.Contains("DELETE FROM [Production].[ProductSubcategory]", Sql.All);
        Assert.All(subcategory.Products, p => Assert.Null(p.ProductSubcategoryId));

        var other = NewContext();
        other.Remove(await other.ProductSubcategories.SingleAsync(s => s.ProductSubcategoryId == 2)); // products not loaded
        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());                 // FK violation
    }

    // Concept 5: SavingChanges, the lightweight cousin of a SaveChangesInterceptor.
    [Fact]
    public async Task SavingChanges_event_sees_the_final_states()
    {
        var states = new List<string>();
        Db.SavingChanges += (sender, _) =>
            states.AddRange(((DbContext)sender!).ChangeTracker.Entries().Select(e => $"{e.Metadata.ClrType.Name} {e.State}"));
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Changed";
        Db.ProductCategories.Add(new ProductCategory { Name = "E08 event" });

        await Db.SaveChangesAsync();

        Assert.Equal(["Product Modified", "ProductCategory Added"], states.Order());
    }
}
