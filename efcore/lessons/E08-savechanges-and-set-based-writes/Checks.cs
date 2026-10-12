using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.E08;

public record ProductPrice(int ProductId, decimal ListPrice);
public record ProductStamp(int ProductId, decimal ListPrice, DateTime ModifiedDate);

// The checks for E08. E08Exercises (yours) and E08Solutions both inherit them.
public abstract class E08Checks : LabTest
{
    protected abstract Task Ex1(AdventureWorksContext db, List<ProductCategory> categories);
    protected abstract Task<int> Ex2(AdventureWorksContext db, int subcategoryId, decimal percent);
    protected abstract Task<int> Ex3(AdventureWorksContext db, int salesOrderId);
    protected abstract Task<List<Product>> Ex4(AdventureWorksContext db, int subcategoryId);
    protected abstract SaveChangesInterceptor Ex5();
    protected abstract Task Ch1(AdventureWorksContext db, int salesOrderId);
    protected abstract Task Ch2(AdventureWorksContext db, int subcategoryId);
    protected abstract Task<int> Ch3(AdventureWorksContext db, int subcategoryId, decimal percent, DateTime now);

    Task<List<ProductPrice>> PricesIn(int subcategoryId) => Reference.Rows<ProductPrice>(
        $"SELECT ProductID AS ProductId, ListPrice FROM Production.Product WHERE ProductSubcategoryID = {subcategoryId} ORDER BY ProductID");

    static List<ProductPrice> Raised(List<ProductPrice> prices, decimal percent)
        => prices.Select(p => p with { ListPrice = p.ListPrice * (1 + percent / 100) }).ToList();

    async Task<int> Count(FormattableString sql) => (await Reference.Rows<int>(sql)).Single();

    [Exercise]
    public async Task Ex1_InsertInOneRoundTrip()
    {
        List<ProductCategory> categories = [new() { Name = "E08 Drones" }, new() { Name = "E08 Scooters" }, new() { Name = "E08 Skates" }];

        await Ex1(Db, categories);

        Sql.AssertRoundTrips(1);
        Assert.All(categories, c => Assert.True(c.ProductCategoryId > 0)); // generated keys came back
        Assert.Equal(3, await Count($"SELECT COUNT(*) AS Value FROM Production.ProductCategory WHERE Name LIKE 'E08 %'"));
    }

    [Exercise]
    public async Task Ex2_RaisePricesInOneStatement()
    {
        var expected = Raised(await PricesIn(1), 10m);

        var rows = await Ex2(Db, 1, 10m);

        Assert.Equal(expected.Count, rows);
        Sql.AssertRoundTrips(1);
        Assert.StartsWith("UPDATE", Sql.Commands[0].TrimStart());
        Assert.Empty(Db.ChangeTracker.Entries()); // nothing loaded
        Assert.Equal(expected, await PricesIn(1));
    }

    [Exercise]
    public async Task Ex3_DeleteInOneStatement()
    {
        var rows = await Ex3(Db, 43697);

        Assert.Equal(2, rows);
        Sql.AssertRoundTrips(1);
        Assert.StartsWith("DELETE", Sql.Commands[0].TrimStart());
        Assert.Equal(0, await Count($"SELECT COUNT(*) AS Value FROM Sales.SalesOrderHeaderSalesReason WHERE SalesOrderID = 43697"));
    }

    [Exercise]
    public async Task Ex4_NoStaleEntities()
    {
        var tracked = await Db.Products.Where(p => p.ProductSubcategoryId == 1).ToListAsync();
        var expected = Raised(await PricesIn(1), 10m);
        Sql.Reset();

        var products = await Ex4(Db, 1);

        Sql.AssertRoundTrips(2); // the UPDATE, then one query
        Assert.Contains("UPDATE", Sql.Commands[0]);
        Assert.Equal(expected, products.OrderBy(p => p.ProductId).Select(p => new ProductPrice(p.ProductId, p.ListPrice)).ToList());
    }

    [Exercise]
    public async Task Ex5_StampModifiedDate()
    {
        var db = NewContext<AdventureWorksContext>(Ex5());
        var product = await db.Products.SingleAsync(p => p.ProductId == 1);
        var untouched = await db.Products.SingleAsync(p => p.ProductId == 2);
        var untouchedDate = untouched.ModifiedDate;
        var before = DateTime.Now;

        product.Name = "Stamped";
        var category = new ProductCategory { Name = "E08 Stamped" };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();

        var after = DateTime.Now;
        Assert.InRange(product.ModifiedDate, before, after);
        Assert.InRange(category.ModifiedDate, before, after);
        Assert.Equal(untouchedDate, untouched.ModifiedDate); // unchanged entities keep their date
        var saved = (await Reference.Rows<ProductStamp>($"SELECT ProductID AS ProductId, ListPrice, ModifiedDate FROM Production.Product WHERE ProductID = 1")).Single();
        Assert.Equal(product.ModifiedDate, saved.ModifiedDate, TimeSpan.FromMilliseconds(5)); // datetime rounds to ~3 ms
    }

    [Exercise]
    public async Task Ch1_LetTheDatabaseCascade()
    {
        await Ch1(Db, 43659);

        Sql.AssertRoundTrips(1);
        Assert.DoesNotContain("[Sales].[SalesOrderDetail]", Sql.All); // the database deleted the lines
        Assert.Equal(0, await Count($"SELECT COUNT(*) AS Value FROM Sales.SalesOrderHeader WHERE SalesOrderID = 43659"));
        Assert.Equal(0, await Count($"SELECT COUNT(*) AS Value FROM Sales.SalesOrderDetail WHERE SalesOrderID = 43659"));
    }

    [Exercise]
    public async Task Ch2_DeleteAParentOfOptionalChildren()
    {
        var products = await Count($"SELECT COUNT(*) AS Value FROM Production.Product");
        var children = await Count($"SELECT COUNT(*) AS Value FROM Production.Product WHERE ProductSubcategoryID = 1");
        var withoutSubcategory = await Count($"SELECT COUNT(*) AS Value FROM Production.Product WHERE ProductSubcategoryID IS NULL");

        await Ch2(Db, 1);

        Assert.InRange(Sql.Count, 1, 2);
        Assert.Equal(0, await Count($"SELECT COUNT(*) AS Value FROM Production.ProductSubcategory WHERE ProductSubcategoryID = 1"));
        Assert.Equal(products, await Count($"SELECT COUNT(*) AS Value FROM Production.Product")); // no product deleted
        Assert.Equal(withoutSubcategory + children, await Count($"SELECT COUNT(*) AS Value FROM Production.Product WHERE ProductSubcategoryID IS NULL"));
    }

    [Exercise]
    public async Task Ch3_SetBasedWritesSkipInterceptors()
    {
        var now = new DateTime(2026, 10, 12, 9, 30, 0);
        var expected = Raised(await PricesIn(1), 5m);

        var rows = await Ch3(Db, 1, 5m, now);

        Assert.Equal(expected.Count, rows);
        Sql.AssertRoundTrips(1);
        var saved = await Reference.Rows<ProductStamp>($"SELECT ProductID AS ProductId, ListPrice, ModifiedDate FROM Production.Product WHERE ProductSubcategoryID = 1 ORDER BY ProductID");
        Assert.Equal(expected, saved.Select(s => new ProductPrice(s.ProductId, s.ListPrice)).ToList());
        Assert.All(saved, s => Assert.Equal(now, s.ModifiedDate));
    }
}
