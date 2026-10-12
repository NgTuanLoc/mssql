using EfCoreLessons.Lab;
using EfCoreLessons.Model;

namespace EfCoreLessons.E02;

public record OrderWithNames(int SalesOrderId, string? CustomerLastName, string? TerritoryName);
public record SalesPersonCountry(int BusinessEntityId, string LastName, string? CountryRegionCode);
public record NotRedCounts(int CSharpSemantics, int SqlSemantics);
public record OrderParty(int SalesOrderId, string Customer, string? SalesPerson);
public record NamedParty(string Name, string Kind);
public record ProductRating(int ProductId, int? Rating);

// The checks for E02. E02Exercises (yours) and E02Solutions both inherit them.
public abstract class E02Checks : LabTest
{
    static readonly DateTime LeapDay = new(2012, 2, 29);

    protected abstract Task<List<OrderWithNames>> Ex1(AdventureWorksContext db, DateTime orderDate);
    protected abstract Task<List<SalesPersonCountry>> Ex2(AdventureWorksContext db);
    protected abstract Task<List<int>> Ex3(AdventureWorksContext db);
    protected abstract Task<NotRedCounts> Ex4(AdventureWorksContext db);
    protected abstract Task<List<int>> Ex5(AdventureWorksContext db, string lastName);
    protected abstract Task<List<OrderParty>> Ch1(AdventureWorksContext db, DateTime orderDate);
    protected abstract Task<List<NamedParty>> Ch2(AdventureWorksContext db, string text);
    protected abstract Task<List<ProductRating>> Ch3(AdventureWorksContext db, int minRating);

    [Exercise]
    public async Task Ex1_OrdersWithNames()
    {
        var result = await Ex1(Db, LeapDay);

        Assert.Equal(await Reference.Rows<OrderWithNames>($"""
            SELECT h.SalesOrderID AS SalesOrderId, p.LastName AS CustomerLastName, t.Name AS TerritoryName
            FROM Sales.SalesOrderHeader h
            JOIN Sales.Customer c ON c.CustomerID = h.CustomerID
            LEFT JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
            LEFT JOIN Sales.SalesTerritory t ON t.TerritoryID = h.TerritoryID
            WHERE h.OrderDate = '2012-02-29'
            ORDER BY h.SalesOrderID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.DoesNotContain("[Freight]", Sql.All);   // no whole-entity loading
        Assert.Empty(Db.ChangeTracker.Entries());
    }

    [Exercise]
    public async Task Ex2_SalesPeopleWithCountry()
    {
        var result = await Ex2(Db);

        Assert.Equal(await Reference.Rows<SalesPersonCountry>($"""
            SELECT v.BusinessEntityID AS BusinessEntityId, v.LastName, t.CountryRegionCode
            FROM Sales.vSalesPerson v
            LEFT JOIN Sales.SalesTerritory t ON t.Name = v.TerritoryName
            ORDER BY v.BusinessEntityID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("LEFT JOIN", Sql.All);
    }

    [Exercise]
    public async Task Ex3_SoldButNeverPurchased()
    {
        var result = await Ex3(Db);

        Assert.Equal(await Reference.Rows<int>($"""
            SELECT ProductID AS Value FROM Sales.SalesOrderDetail
            EXCEPT
            SELECT ProductID FROM Purchasing.PurchaseOrderDetail
            ORDER BY Value
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("EXCEPT", Sql.All);
    }

    [Exercise]
    public async Task Ex4_NotRedTwoWays()
    {
        var result = await Ex4(Db);

        var cSharp = (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Production.Product WHERE Color <> 'Red' OR Color IS NULL")).Single();
        var tSql = (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Production.Product WHERE Color <> 'Red'")).Single();
        Assert.Equal(new NotRedCounts(cSharp, tSql), result);
        Assert.InRange(Sql.Count, 1, 2);
        Assert.All(Sql.Commands, sql => Assert.Contains("COUNT(", sql)); // counted in SQL, no rows fetched
    }

    [Exercise]
    public async Task Ex5_LastNameIgnoringCase()
    {
        var result = await Ex5(Db, "SMITH");

        Assert.Equal(await Reference.Rows<int>($"""
            SELECT BusinessEntityID AS Value FROM Person.Person WHERE LastName = 'Smith' ORDER BY BusinessEntityID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.DoesNotContain("UPPER(", Sql.All);
        Assert.DoesNotContain("LOWER(", Sql.All);
    }

    [Exercise]
    public async Task Ch1_OrderParties()
    {
        var result = await Ch1(Db, LeapDay);

        Assert.Equal(await Reference.Rows<OrderParty>($"""
            SELECT h.SalesOrderID AS SalesOrderId,
                   COALESCE(s.Name, p.LastName + N', ' + p.FirstName) AS Customer,
                   sp.FirstName + N' ' + sp.LastName AS SalesPerson
            FROM Sales.SalesOrderHeader h
            JOIN Sales.Customer c ON c.CustomerID = h.CustomerID
            LEFT JOIN Sales.Store s ON s.BusinessEntityID = c.StoreID
            LEFT JOIN Person.Person p ON p.BusinessEntityID = c.PersonID
            LEFT JOIN Person.Person sp ON sp.BusinessEntityID = h.SalesPersonID
            WHERE h.OrderDate = '2012-02-29'
            ORDER BY h.SalesOrderID
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("CASE", Sql.All); // the strings are built by SQL, not in memory
    }

    [Exercise]
    public async Task Ch2_VendorsAndStores()
    {
        var result = await Ch2(Db, "Bike");

        Assert.Equal(await Reference.Rows<NamedParty>($"""
            SELECT Name, N'Vendor' AS Kind FROM Purchasing.Vendor WHERE Name LIKE N'%Bike%'
            UNION ALL
            SELECT Name, N'Store' FROM Sales.Store WHERE Name LIKE N'%Bike%'
            ORDER BY Name, Kind
            """), result);
        Sql.AssertRoundTrips(1);
        Assert.Contains("UNION ALL", Sql.All);
    }

    [Exercise]
    public async Task Ch3_RatingsWithoutLosingProducts()
    {
        var result = await Ch3(Db, 4);

        Assert.Equal(await Reference.Rows<ProductRating>($"""
            SELECT p.ProductID AS ProductId, r.Rating
            FROM Production.Product p
            LEFT JOIN Production.ProductReview r ON r.ProductID = p.ProductID AND r.Rating >= 4
            ORDER BY p.ProductID, r.Rating
            """), result);
        Sql.AssertRoundTrips(1);
    }
}
