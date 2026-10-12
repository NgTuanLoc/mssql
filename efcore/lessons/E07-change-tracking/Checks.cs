using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E07;

public record PropertyChange(string Property, object? Original, object? Current);
public record ProductEdit(int ProductId, string Name, decimal ListPrice, string? Color);

// The checks for E07. E07Exercises (yours) and E07Solutions both inherit them.
public abstract class E07Checks : LabTest
{
    protected abstract List<PropertyChange> Ex1(AdventureWorksContext db, Product product);
    protected abstract Task Ex2(AdventureWorksContext db, int productId, decimal listPrice);
    protected abstract Task Ex3(AdventureWorksContext db, ProductEdit edit);
    protected abstract bool Ex4(AdventureWorksContext db);
    protected abstract Task Ex5(AdventureWorksContext db, Product product);
    protected abstract Task Ch1(AdventureWorksContext db, Product newProduct, ProductSubcategory detachedSubcategory);
    protected abstract Task Ch2(AdventureWorksContext db, SalesOrderHeader detachedOrder, int changedDetailId);
    protected abstract Task Ch3(AdventureWorksContext db, Product staleCopy);

    // A new, valid product that belongs to no subcategory yet.
    protected static Product NewProduct(string productNumber) => new()
    {
        Name = $"E07 test product {productNumber}",
        ProductNumber = productNumber,
        SafetyStockLevel = 1,
        ReorderPoint = 1,
        StandardCost = 10m,
        ListPrice = 20m,
        DaysToManufacture = 0,
        SellStartDate = new DateTime(2026, 1, 1),
    };

    async Task<int> SubcategoryCount() =>
        (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Production.ProductSubcategory")).Single();

    static int Occurrences(string text, string part) => text.Split(part).Length - 1;

    [Exercise]
    public async Task Ex1_WhatChanged()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Adjustable Race v2";
        product.ListPrice = 12.5m;
        product.Color = null; // assigned, but the same value: not a change
        Sql.Reset();

        var changes = Ex1(Db, product);

        Assert.Equal(
            [new("ListPrice", 0m, 12.5m), new("Name", "Adjustable Race", "Adjustable Race v2")],
            changes);
        Assert.Equal(0, Sql.Count); // the tracker already knows: no query
    }

    [Exercise]
    public async Task Ex2_UpdateWithoutLoading()
    {
        await Ex2(Db, 1, 12.5m);

        Sql.AssertRoundTrips(1);
        Assert.Contains("SET [ListPrice] = @", Sql.All);
        Assert.DoesNotContain("[Name] = @", Sql.All); // only the column you changed
        var row = (await Reference.Rows<ProductEdit>($"SELECT ProductID AS ProductId, Name, ListPrice, Color FROM Production.Product WHERE ProductID = 1")).Single();
        Assert.Equal(new ProductEdit(1, "Adjustable Race", 12.5m, null), row);
    }

    [Exercise]
    public async Task Ex3_ApplyAnEdit()
    {
        await Ex3(Db, new ProductEdit(1, "Adjustable Race v2", 12.5m, null));

        Sql.AssertRoundTrips(2); // load, then save
        var update = Sql.Commands[1];
        Assert.Contains("[Name] = @", update);
        Assert.Contains("[ListPrice] = @", update);
        Assert.DoesNotContain("[Color] = @", update); // unchanged values are not written
        var row = (await Reference.Rows<ProductEdit>($"SELECT ProductID AS ProductId, Name, ListPrice, Color FROM Production.Product WHERE ProductID = 1")).Single();
        Assert.Equal(new ProductEdit(1, "Adjustable Race v2", 12.5m, null), row);
    }

    [Exercise]
    public async Task Ex4_AnythingToSave()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);

        product.Name = "Adjustable Race"; // the value it already had
        Assert.False(Ex4(Db));

        product.ListPrice = 1m;
        Assert.True(Ex4(Db));

        Sql.AssertRoundTrips(1); // only the load
    }

    [Exercise]
    public async Task Ex5_DiscardLocalChanges()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Not saved";
        Sql.Reset();

        await Ex5(Db, product);

        Sql.AssertRoundTrips(1);
        Assert.Equal(EntityState.Unchanged, Db.Entry(product).State);
        Assert.Equal("Adjustable Race", product.Name);
    }

    [Exercise]
    public async Task Ch1_NewProductInAnExistingSubcategory()
    {
        var subcategory = await NewContext().ProductSubcategories.AsNoTracking().SingleAsync(s => s.ProductSubcategoryId == 1);
        var before = await SubcategoryCount();
        var product = NewProduct("E07-CH1");
        Sql.Reset();

        await Ch1(Db, product, subcategory);

        Assert.True(product.ProductId > 0, "The new product should have been saved (it gets its ProductId from the database).");
        Assert.DoesNotContain("INSERT INTO [Production].[ProductSubcategory]", Sql.All);
        Assert.Equal(before, await SubcategoryCount());
        Assert.Equal(1, (await Reference.Rows<int>($"SELECT ProductSubcategoryID AS Value FROM Production.Product WHERE ProductID = {product.ProductId}")).Single());
    }

    [Exercise]
    public async Task Ch2_SaveADisconnectedGraph()
    {
        var order = await NewContext().SalesOrderHeaders.AsNoTracking()
            .Include(h => h.SalesOrderDetails)
            .SingleAsync(h => h.SalesOrderId == 43659);
        var lines = order.SalesOrderDetails.Count;
        var changed = order.SalesOrderDetails.OrderBy(d => d.SalesOrderDetailId).First();
        changed.OrderQty += 5;
        order.SalesOrderDetails.Add(new SalesOrderDetail
        {
            SalesOrderId = order.SalesOrderId,
            ProductId = changed.ProductId,
            SpecialOfferId = changed.SpecialOfferId,
            OrderQty = 1,
            UnitPrice = changed.UnitPrice,
        });
        Sql.Reset();

        await Ch2(Db, order, changed.SalesOrderDetailId);

        var sql = Sql.All;
        Assert.Equal(1, Occurrences(sql, "UPDATE [Sales].[SalesOrderDetail]"));    // only the changed line
        Assert.Equal(1, Occurrences(sql, "INSERT INTO [Sales].[SalesOrderDetail]")); // the new line
        Assert.DoesNotContain("UPDATE [Sales].[SalesOrderHeader]", sql);             // the header didn't change
        Assert.Equal(lines + 1, (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43659")).Single());
        Assert.Equal(changed.OrderQty, (await Reference.Rows<short>($"SELECT OrderQty AS Value FROM Sales.SalesOrderDetail WHERE SalesOrderDetailID = {changed.SalesOrderDetailId}")).Single());
    }

    [Exercise]
    public async Task Ch3_MergeIntoTheTrackedInstance()
    {
        var tracked = await Db.Products.SingleAsync(p => p.ProductId == 1);
        tracked.Name = "Renamed locally";
        var staleCopy = await NewContext().Products.AsNoTracking().SingleAsync(p => p.ProductId == 1);
        staleCopy.ListPrice = 99m;
        Sql.Reset();

        await Ch3(Db, staleCopy);

        Assert.Equal(0, Sql.Count); // no query, no save
        await Db.SaveChangesAsync();
        var row = (await Reference.Rows<ProductEdit>($"SELECT ProductID AS ProductId, Name, ListPrice, Color FROM Production.Product WHERE ProductID = 1")).Single();
        Assert.Equal(new ProductEdit(1, "Renamed locally", 99m, null), row); // both changes kept
    }
}
