using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E13;

// One territory's sales in one month, with the previous month's. The examples' own report row.
public class TerritoryMonth
{
    public int TerritoryId { get; set; }
    public DateTime OrderMonth { get; set; }
    public decimal Revenue { get; set; }
    public decimal? PrevRevenue { get; set; }
}

// The examples' context: TerritoryMonth mapped to a SQL query (concept 4). VendorMonth stays unmapped.
public class E13ExampleContext(DbContextOptions<AdventureWorksContext> options) : E13Context(options)
{
    protected override void MapVendorMonths(ModelBuilder modelBuilder)
        => modelBuilder.Entity<TerritoryMonth>().HasNoKey().ToSqlQuery("""
            SELECT TerritoryID AS TerritoryId, OrderMonth, Revenue,
                   LAG(Revenue) OVER (PARTITION BY TerritoryID ORDER BY OrderMonth) AS PrevRevenue
            FROM (
                SELECT TerritoryID, DATEFROMPARTS(YEAR(OrderDate), MONTH(OrderDate), 1) AS OrderMonth, SUM(TotalDue) AS Revenue
                FROM Sales.SalesOrderHeader
                GROUP BY TerritoryID, DATEFROMPARTS(YEAR(OrderDate), MONTH(OrderDate), 1)
            ) AS m
            """);
}

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E13Examples --logger "console;verbosity=detailed"
public class E13Examples : LabTest<E13ExampleContext>
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    static bool IsBlue(string? color) => color == "Blue";

    // Concept 1: a filter EF can't translate throws; it never quietly runs in memory.
    [Fact]
    public async Task Untranslatable_filters_throw()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Db.Products.Where(p => IsBlue(p.Color)).ToListAsync());

        Print(failure.Message);
        Assert.Contains("could not be translated", failure.Message);
        Assert.Equal(0, Sql.Count); // nothing was sent
    }

    // Concept 2: an IQueryable is a query, not data. Naming a step and using it twice is like a CTE
    // referenced twice: one round trip, the step inlined in both places.
    [Fact]
    public async Task A_named_IQueryable_works_like_a_CTE()
    {
        var territoryTotals = Db.SalesOrderHeaders
            .GroupBy(h => h.TerritoryId)
            .Select(g => new { TerritoryId = g.Key, Total = g.Sum(h => h.TotalDue) });

        var aboveAverage = await territoryTotals
            .Where(t => t.Total > territoryTotals.Average(x => x.Total))
            .CountAsync();

        Print($"{aboveAverage} territories above the average");
        Sql.AssertRoundTrips(1);
        Assert.Equal(2, Occurrences(Sql.All, "GROUP BY [s"));
    }

    // Concept 3: DefaultIfEmpty keeps left rows with no matches (OUTER APPLY's job). On 2014-06-30 only
    // some territories took orders; with DefaultIfEmpty every territory is still there.
    [Fact]
    public async Task DefaultIfEmpty_keeps_rows_without_matches()
    {
        var day = new DateTime(2014, 6, 30);
        var withOrders = await Db.SalesTerritories
            .SelectMany(t => t.SalesOrderHeaders.Where(h => h.OrderDate == day).OrderByDescending(h => h.SalesOrderId).Take(1))
            .CountAsync();
        var all = await Db.SalesTerritories
            .SelectMany(
                t => t.SalesOrderHeaders.Where(h => h.OrderDate == day).OrderByDescending(h => h.SalesOrderId).Take(1).DefaultIfEmpty(),
                (t, h) => new { t.Name, SalesOrderId = (int?)h!.SalesOrderId })
            .ToListAsync();

        Print($"{withOrders} territories with orders that day; {all.Count(x => x.SalesOrderId == null)} without");
        Assert.True(withOrders < 10);
        Assert.Equal(10, all.Count);
        Assert.Contains("LEFT JOIN", Sql.Commands[1]); // a left join over ROW_NUMBER(): OUTER APPLY by another route
    }

    // Concept 4: a keyless entity mapped with ToSqlQuery: SQL for the part LINQ can't say (LAG),
    // LINQ composed on top for the rest.
    [Fact]
    public async Task ToSqlQuery_maps_a_report_LINQ_can_compose_on()
    {
        var northwest = await Db.Set<TerritoryMonth>()
            .Where(m => m.TerritoryId == 1 && m.PrevRevenue != null)
            .OrderBy(m => m.OrderMonth)
            .Select(m => new { m.OrderMonth, Growth = Math.Round(100 * (m.Revenue - m.PrevRevenue!.Value) / m.PrevRevenue.Value, 1) })
            .Take(3)
            .ToListAsync();

        foreach (var month in northwest) Print(month);
        Sql.AssertRoundTrips(1);
        Assert.Contains("LAG(Revenue)", Sql.All);  // your SQL, as a subquery
        Assert.Contains(") AS [e]", Sql.All);       // ...wrapped as a derived table, like FromSql (E04 concept 2)
        Assert.Contains("ROUND(", Sql.All);         // ...with the arithmetic translated around it
    }

    // Concept 5: a conditional UPDATE checks and changes a row in one statement; its row count says
    // whether the condition held.
    [Fact]
    public async Task A_conditional_UPDATE_checks_and_takes_atomically()
    {
        Task<int> Take(short quantity) => Db.ProductInventories
            .Where(i => i.ProductId == 709 && i.LocationId == 7 && i.Quantity >= quantity) // 180 in stock
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.Quantity, i => (short)(i.Quantity - quantity)));

        Assert.Equal(1, await Take(150)); // 30 left
        Assert.Equal(0, await Take(50));  // not enough: nothing changed
        Assert.Equal(30, await Db.ProductInventories.Where(i => i.ProductId == 709 && i.LocationId == 7).Select(i => i.Quantity).SingleAsync());
    }

    static int Occurrences(string text, string value) => (text.Length - text.Replace(value, "").Length) / value.Length;
}
