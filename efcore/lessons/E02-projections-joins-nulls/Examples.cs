using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCoreLessons.E02;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E02Examples --logger "console;verbosity=detailed"
public class E02Examples : LabTest
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: an entity query selects every column and tracks; a projection selects what it uses.
    [Fact]
    public async Task Projection_selects_only_the_columns_it_uses()
    {
        await Db.Products.Where(p => p.ListPrice > 3000).ToListAsync();
        var tracked = Db.ChangeTracker.Entries().Count();
        Db.ChangeTracker.Clear();

        await Db.Products.Where(p => p.ListPrice > 3000).Select(p => new { p.ProductId, p.Name }).ToListAsync();

        Assert.Contains("[p].[WeightUnitMeasureCode]", Sql.Commands[0]);
        Assert.DoesNotContain("[p].[WeightUnitMeasureCode]", Sql.Commands[1]);
        Assert.True(tracked > 0);
        Assert.Empty(Db.ChangeTracker.Entries());
    }

    // Concept 2: required relationship -> INNER JOIN, optional relationship -> LEFT JOIN.
    [Fact]
    public async Task Required_navigation_is_INNER_JOIN_optional_is_LEFT_JOIN()
    {
        await Db.SalesOrderHeaders
            .Where(h => h.SalesOrderId == 43659)
            .Select(h => new { h.Customer.AccountNumber, Territory = h.Territory!.Name })
            .SingleAsync();

        Assert.Contains("INNER JOIN [Sales].[Customer]", Sql.All);
        Assert.Contains("LEFT JOIN [Sales].[SalesTerritory]", Sql.All);
    }

    // Concept 3: keyless views have no navigations, so join them explicitly.
    [Fact]
    public async Task Join_when_there_is_no_navigation()
    {
        var rows = await Db.VSalesPeople
            .Join(Db.SalesPeople, v => v.BusinessEntityId, s => s.BusinessEntityId,
                (v, s) => new { v.LastName, s.CommissionPct })
            .ToListAsync();

        Assert.Equal(17, rows.Count);
        Assert.Contains("INNER JOIN", Sql.All);
    }

    // Concept 3: EF 10's LeftJoin/RightJoin, and the GroupJoin pattern they replace.
    [Fact]
    public async Task LeftJoin_RightJoin_and_GroupJoin()
    {
        var left = await Db.VSalesPeople
            .LeftJoin(Db.SalesTerritories, v => v.TerritoryName, t => t.Name,
                (v, t) => new { v.LastName, Code = t == null ? null : t.CountryRegionCode })
            .ToListAsync();

        var right = await Db.SalesTerritories
            .RightJoin(Db.VSalesPeople, t => t.Name, v => v.TerritoryName,
                (t, v) => new { v.LastName, Code = t == null ? null : t.CountryRegionCode })
            .ToListAsync();

        var classic = await Db.VSalesPeople
            .GroupJoin(Db.SalesTerritories, v => v.TerritoryName, t => t.Name, (v, ts) => new { v, ts })
            .SelectMany(x => x.ts.DefaultIfEmpty(), (x, t) => new { x.v.LastName, Code = t == null ? null : t.CountryRegionCode })
            .ToListAsync();

        Assert.Equal(17, left.Count);
        Assert.Equal(3, left.Count(x => x.Code == null)); // sales people without a territory
        Assert.Equal(17, right.Count);
        Assert.Equal(17, classic.Count);
        Assert.Contains("LEFT JOIN", Sql.Commands[0]);
        Assert.Contains("RIGHT JOIN", Sql.Commands[1]);
        Assert.Contains("LEFT JOIN", Sql.Commands[2]);
    }

    // Concept 4: two different stores are both called "Friendly Bike Shop".
    [Fact]
    public async Task Union_removes_duplicates_Concat_keeps_them()
    {
        var stores = Db.Stores.Where(s => s.Name == "Friendly Bike Shop").Select(s => s.Name);
        var vendors = Db.Vendors.Where(v => v.Name == "Friendly Bike Shop").Select(v => v.Name);

        var union = await stores.Union(vendors).ToListAsync();
        var concat = await stores.Concat(vendors).ToListAsync();

        Assert.Single(union);
        Assert.Equal(2, concat.Count);
        Assert.DoesNotContain("UNION ALL", Sql.Commands[0]);
        Assert.Contains("UNION", Sql.Commands[0]);
        Assert.Contains("UNION ALL", Sql.Commands[1]);
    }

    // Concept 1: anything composed after a positional-record projection can't be translated.
    [Fact]
    public async Task Composing_after_a_record_projection_fails()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Db.Products.Select(p => new ProductName(p.ProductId, p.Name)).OrderBy(x => x.Name).ToListAsync());
        Print(error.Message);

        var works = await Db.Products
            .Select(p => new { p.ProductId, p.Name })
            .OrderBy(x => x.Name)
            .Select(x => new ProductName(x.ProductId, x.Name))
            .ToListAsync();
        Assert.Equal(504, works.Count);
    }

    record ProductName(int ProductId, string Name);

    // Concept 5: C# says null != "Red"; EF adds OR IS NULL to agree. Hand-written T-SQL returns 218.
    [Fact]
    public async Task Not_equal_keeps_NULL_rows_unlike_TSQL()
    {
        Assert.Equal(466, await Db.Products.CountAsync(p => p.Color != "Red"));
        Assert.Equal(218, await Db.Products.CountAsync(p => p.Color != null && p.Color != "Red"));

        Assert.Contains("[p].[Color] <> N'Red' OR [p].[Color] IS NULL", Sql.Commands[0]);
    }

    // Concept 5: EF looks at the parameter's value; a null variable becomes IS NULL.
    [Fact]
    public async Task Null_variable_becomes_IS_NULL()
    {
        string? color = null;
        var noColor = await Db.Products.CountAsync(p => p.Color == color);

        Assert.Equal(248, noColor);
        Assert.Contains("[p].[Color] IS NULL", Sql.All);
    }

    // Concept 5: comparing two nullable columns treats NULL = NULL as true, like C#.
    [Fact]
    public async Task Two_nullable_columns_compare_with_null_compensation()
    {
        await Db.Products.CountAsync(p => p.SizeUnitMeasureCode == p.WeightUnitMeasureCode);

        Assert.Contains("OR ([p].[SizeUnitMeasureCode] IS NULL AND [p].[WeightUnitMeasureCode] IS NULL)", Sql.All);
    }

    // Concept 5: UseRelationalNulls turns the compensation off: same LINQ, T-SQL's answer.
    [Fact]
    public async Task UseRelationalNulls_switches_to_TSQL_semantics()
    {
        var options = new DbContextOptionsBuilder<AdventureWorksContext>()
            .UseSqlServer(Db.Database.GetDbConnection(), sql => sql.UseRelationalNulls())
            .Options;
        await using var relational = new AdventureWorksContext(options);
        relational.Database.UseTransaction(Db.Database.CurrentTransaction!.GetDbTransaction());

        Assert.Equal(218, await relational.Products.CountAsync(p => p.Color != "Red"));
    }

    // Concept 6: the column's collation is case-insensitive, so == ignores case; StringComparison throws.
    [Fact]
    public async Task Collation_decides_case_sensitivity()
    {
        Assert.Equal(103, await Db.People.CountAsync(p => p.LastName == "SMITH"));
        Assert.Equal(0, await Db.People.CountAsync(p =>
            EF.Functions.Collate(p.LastName, "SQL_Latin1_General_CP1_CS_AS") == "SMITH"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Db.People.CountAsync(p => string.Equals(p.LastName, "SMITH", StringComparison.OrdinalIgnoreCase)));
        Print(error.Message);
    }
}
