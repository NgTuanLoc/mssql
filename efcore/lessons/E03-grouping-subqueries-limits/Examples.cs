using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E03;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E03Examples --logger "console;verbosity=detailed"
public class E03Examples : LabTest
{
    static readonly DateTime From = new(2013, 1, 1);
    static readonly DateTime To = new(2014, 1, 1);

    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: key + aggregates in the projection -> GROUP BY.
    [Fact]
    public async Task GroupBy_with_aggregates_is_GROUP_BY()
    {
        var groups = await Db.SalesOrderHeaders
            .Where(h => h.OrderDate >= From && h.OrderDate < To)
            .GroupBy(h => h.Territory!.Group)
            .Select(g => new { Group = g.Key, Orders = g.Count(), Total = g.Sum(h => h.TotalDue) })
            .ToListAsync();

        Assert.Equal(3, groups.Count); // North America, Europe, Pacific
        Assert.Contains("GROUP BY [s0].[Group]", Sql.All);
    }

    // Concept 1: a predicate inside Count/Sum becomes CASE inside the aggregate.
    [Fact]
    public async Task Conditional_aggregates_become_CASE()
    {
        var mix = await Db.SalesOrderHeaders
            .GroupBy(h => 1)
            .Select(g => new
            {
                Online = g.Count(h => h.OnlineOrderFlag),
                Offline = g.Count(h => !h.OnlineOrderFlag),
                OnlineTotal = g.Sum(h => h.OnlineOrderFlag ? h.TotalDue : 0),
            })
            .SingleAsync();

        Print(mix);
        Assert.Equal(31465, mix.Online + mix.Offline);
        Assert.Contains("COUNT(CASE", Sql.All);
    }

    // Concept 1: Where after GroupBy filters groups -> HAVING.
    [Fact]
    public async Task Where_after_GroupBy_is_HAVING()
    {
        var loyal = await Db.SalesOrderHeaders
            .GroupBy(h => h.CustomerId)
            .Where(g => g.Count() >= 25)
            .Select(g => new { CustomerId = g.Key, Orders = g.Count() })
            .ToListAsync();

        Assert.Equal(14, loyal.Count);
        Assert.Contains("HAVING COUNT(*) >= 25", Sql.All);
    }

    // Concept 2: returning the groups themselves: ORDER BY + grouping on the client.
    [Fact]
    public async Task GroupBy_as_last_operator_groups_on_the_client()
    {
        var byColor = await Db.Products.Where(p => p.Color != null).GroupBy(p => p.Color).ToListAsync();

        Assert.Equal(9, byColor.Count);
        Assert.DoesNotContain("GROUP BY", Sql.All);
        Assert.Contains("ORDER BY [p].[Color]", Sql.All);
    }

    // Concept 3: an aggregate over a navigation is a correlated scalar subquery.
    [Fact]
    public async Task Navigation_aggregate_is_a_correlated_subquery()
    {
        var customers = await Db.Customers
            .Where(c => c.CustomerId < 11005)
            .Select(c => new { c.CustomerId, Orders = c.SalesOrderHeaders.Count() })
            .ToListAsync();

        Print(string.Join(", ", customers));
        Assert.Contains("WHERE [c].[CustomerID] = [s].[CustomerID]) AS [Orders]", Sql.All);
    }

    // Concept 3: Any -> EXISTS, !Any -> NOT EXISTS, All -> NOT EXISTS of the negated predicate.
    [Fact]
    public async Task Any_and_All_are_EXISTS_and_NOT_EXISTS()
    {
        var neverOrdered = await Db.Products
            .CountAsync(p => p.FinishedGoodsFlag && !Db.SalesOrderDetails.Any(d => d.ProductId == p.ProductId));
        var allPricey = await Db.ProductSubcategories
            .Where(s => s.Products.All(p => p.ListPrice > 100))
            .Select(s => s.Name)
            .ToListAsync();

        Print($"{neverOrdered} finished goods never ordered; all over $100: {string.Join(", ", allPricey)}");
        Assert.Contains("NOT EXISTS", Sql.Commands[0]);
        Assert.Contains("NOT EXISTS", Sql.Commands[1]);
        Assert.Contains("<= 100.0", Sql.Commands[1]); // All(> 100) became "no product <= 100"
    }

    // Concept 3: Contains over another query -> IN (SELECT ...).
    [Fact]
    public async Task Contains_on_a_subquery_is_IN()
    {
        var bulkOrdered = Db.SalesOrderDetails.Where(d => d.OrderQty > 30).Select(d => d.ProductId);
        await Db.Products.Where(p => bulkOrdered.Contains(p.ProductId)).Select(p => p.Name).ToListAsync();

        Assert.Matches(@"IN \(\s+SELECT", Sql.All);
        Sql.AssertRoundTrips(1);
    }

    // Concept 4: EF 10 pads list parameters; EF.Parameter forces OPENJSON, EF.Constant inlines.
    [Fact]
    public async Task Parameterized_collections_are_padded()
    {
        var five = new List<int> { 1, 2, 3, 4, 5 };
        var six = new List<int> { 1, 2, 3, 4, 5, 6 };
        var eleven = Enumerable.Range(1, 11).ToList();
        var many = Enumerable.Range(1, 3000).ToList();

        await Db.Products.CountAsync(p => five.Contains(p.ProductId));
        await Db.Products.CountAsync(p => six.Contains(p.ProductId));
        await Db.Products.CountAsync(p => eleven.Contains(p.ProductId));
        await Db.Products.CountAsync(p => many.Contains(p.ProductId));
        await Db.Products.CountAsync(p => EF.Parameter(six).Contains(p.ProductId));
        await Db.Products.CountAsync(p => EF.Constant(six).Contains(p.ProductId));

        Assert.Contains("@five5)", Sql.Commands[0]);      // exactly five parameters
        Assert.Contains("@six10)", Sql.Commands[1]);      // six values, padded to ten parameters
        Assert.Contains("@eleven20)", Sql.Commands[2]);   // eleven values, padded to twenty
        Assert.Contains("OPENJSON(@many)", Sql.Commands[3]); // too many for parameters: one JSON parameter
        Assert.Contains("OPENJSON(@six)", Sql.Commands[4]);
        Assert.Contains("IN (1, 2, 3, 4, 5, 6)", Sql.Commands[5]);
    }

    // Concept 5: the two top-N-per-group shapes.
    [Fact]
    public async Task Top_N_per_group_CROSS_APPLY_or_ROW_NUMBER()
    {
        await Db.SalesTerritories
            .SelectMany(t => t.SalesOrderHeaders
                .Where(h => h.OrderDate >= From && h.OrderDate < To)
                .OrderByDescending(h => h.TotalDue)
                .Take(3)
                .Select(h => new { t.Name, h.SalesOrderId, h.TotalDue }))
            .ToListAsync();

        await Db.SalesOrderHeaders
            .Where(h => h.OrderDate >= From && h.OrderDate < To)
            .GroupBy(h => h.TerritoryId)
            .Select(g => g.OrderByDescending(h => h.TotalDue).Take(3).Select(h => h.SalesOrderId))
            .ToListAsync();

        Assert.Contains("CROSS APPLY", Sql.Commands[0]);
        Assert.Contains("ROW_NUMBER() OVER(PARTITION BY [s0].[TerritoryID] ORDER BY [s0].[TotalDue] DESC)", Sql.Commands[1]);
    }

    // Concept 6: no LINQ for OVER(...). Fetch ordered rows once, finish the running total in C#.
    [Fact]
    public async Task Window_functions_have_no_LINQ_form()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Db.Products.OrderBy(p => p.ProductId).Select((p, index) => new { p.Name, index }).ToListAsync());

        var orders = await Db.SalesOrderHeaders
            .Where(h => h.CustomerId == 11091)
            .OrderBy(h => h.OrderDate).ThenBy(h => h.SalesOrderId)
            .Select(h => new { h.SalesOrderId, h.TotalDue })
            .ToListAsync();
        var running = 0m;
        var withRunningTotal = orders.Select(o => new { o.SalesOrderId, Running = running += o.TotalDue }).ToList();

        Assert.Equal(28, withRunningTotal.Count);
        Sql.AssertRoundTrips(1);
    }
}
