using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E07;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E07Examples --logger "console;verbosity=detailed"
public class E07Examples : LabTest
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: an entity's state moves through Added, Unchanged, Modified, Deleted and Detached.
    [Fact]
    public async Task Entity_states_through_a_lifecycle()
    {
        var category = new ProductCategory { Name = "E07 lifecycle" };
        Assert.Equal(EntityState.Detached, Db.Entry(category).State);

        Db.ProductCategories.Add(category);
        Assert.Equal(EntityState.Added, Db.Entry(category).State);

        await Db.SaveChangesAsync();                        // INSERT; the generated key flows back
        Assert.Equal(EntityState.Unchanged, Db.Entry(category).State);
        Assert.True(category.ProductCategoryId > 0);

        category.Name = "E07 lifecycle, renamed";
        Assert.Equal(EntityState.Modified, Db.Entry(category).State);
        await Db.SaveChangesAsync();                        // UPDATE

        Db.ProductCategories.Remove(category);
        Assert.Equal(EntityState.Deleted, Db.Entry(category).State);
        await Db.SaveChangesAsync();                        // DELETE
        Assert.Equal(EntityState.Detached, Db.Entry(category).State);
        Sql.AssertRoundTrips(3);
    }

    // Concept 2: changes are found by comparing with a snapshot, when DetectChanges runs.
    [Fact]
    public async Task Changes_are_detected_against_a_snapshot()
    {
        Db.ChangeTracker.AutoDetectChangesEnabled = false;
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);

        product.Name = "Changed in memory";
        Assert.Equal(EntityState.Unchanged, Db.Entry(product).State); // nothing compared yet
        Assert.Equal(0, await Db.SaveChangesAsync());                  // so nothing is saved

        Db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Modified, Db.Entry(product).State);
        Assert.Equal(1, await Db.SaveChangesAsync());
    }

    // Concept 2: DebugView prints what the tracker knows, including original values.
    [Fact]
    public async Task DebugView_shows_the_tracker()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Changed in memory";
        Db.ChangeTracker.DetectChanges();

        var view = Db.ChangeTracker.DebugView.LongView;
        Print(view);
        Assert.Contains("Product {ProductId: 1} Modified", view);
        Assert.Contains("Name: 'Changed in memory' Modified Originally 'Adjustable Race'", view);
    }

    // Concept 3: one instance per key per context, and a second instance can't be tracked.
    [Fact]
    public async Task Identity_resolution_one_instance_per_key()
    {
        var first = await Db.Products.SingleAsync(p => p.ProductId == 1);
        first.Name = "Edited";
        var second = await Db.Products.SingleAsync(p => p.ProductId == 1);

        Assert.Same(first, second);
        Assert.Equal("Edited", second.Name); // the query did not overwrite the tracked instance

        var failure = Assert.Throws<InvalidOperationException>(() => Db.Products.Attach(new Product { ProductId = 1 }));
        Assert.Contains("another instance with the same key value", failure.Message);
    }

    // Concept 4: only modified properties are written.
    [Fact]
    public async Task Only_modified_columns_are_updated()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Only this column";
        Sql.Reset();

        await Db.SaveChangesAsync();

        Assert.Contains("UPDATE [Production].[Product] SET [Name] = @p0", Sql.All);
        Assert.DoesNotContain("[ListPrice] = @", Sql.All);
    }

    // Concept 5: Attach starts as Unchanged; Update marks every property modified.
    [Fact]
    public async Task Attach_vs_Update()
    {
        var detached = await NewContext().Products.AsNoTracking().SingleAsync(p => p.ProductId == 1);
        detached.ListPrice = 9.99m;
        Sql.Reset();

        var updating = NewContext();
        updating.Products.Update(detached);
        await updating.SaveChangesAsync();
        Assert.Contains("[Name] = @", Sql.All);  // every column rewritten
        Assert.Contains("[Color] = @", Sql.All);

        Sql.Reset();
        var attaching = NewContext();
        var stub = new Product { ProductId = 1 };
        attaching.Products.Attach(stub);
        stub.ListPrice = 19.99m;
        await attaching.SaveChangesAsync();
        Assert.Contains("SET [ListPrice] = @", Sql.All); // one column
        Assert.DoesNotContain("[Name] = @", Sql.All);
    }

    // Concept 6: Add marks every untracked entity in the graph Added, existing ones included.
    [Fact]
    public async Task Add_on_a_graph_inserts_everything_it_reaches()
    {
        var subcategory = await NewContext().ProductSubcategories.AsNoTracking().SingleAsync(s => s.ProductSubcategoryId == 1);
        var product = new Product
        {
            Name = "E07 graph", ProductNumber = "E07-GRAPH", SafetyStockLevel = 1, ReorderPoint = 1,
            StandardCost = 1m, ListPrice = 2m, SellStartDate = new DateTime(2026, 1, 1),
            ProductSubcategory = subcategory,
        };

        Db.Products.Add(product);
        Print(Db.ChangeTracker.DebugView.ShortView);

        Assert.Equal(EntityState.Added, Db.Entry(subcategory).State); // the existing subcategory too
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());
        Assert.Contains("IDENTITY_INSERT", failure.InnerException!.Message);
    }

    // Concept 6: TrackGraph lets you choose each entity's state.
    [Fact]
    public async Task TrackGraph_decides_per_entity()
    {
        var subcategory = await NewContext().ProductSubcategories.AsNoTracking().SingleAsync(s => s.ProductSubcategoryId == 1);
        var product = new Product
        {
            Name = "E07 track graph", ProductNumber = "E07-TRACK", SafetyStockLevel = 1, ReorderPoint = 1,
            StandardCost = 1m, ListPrice = 2m, SellStartDate = new DateTime(2026, 1, 1),
            ProductSubcategory = subcategory,
        };

        Db.ChangeTracker.TrackGraph(product, node =>
            node.Entry.State = node.Entry.IsKeySet ? EntityState.Unchanged : EntityState.Added);

        Assert.Equal(EntityState.Unchanged, Db.Entry(subcategory).State);
        Assert.Equal(EntityState.Added, Db.Entry(product).State);
        await Db.SaveChangesAsync();
        Assert.DoesNotContain("INSERT INTO [Production].[ProductSubcategory]", Sql.All);
    }
}
