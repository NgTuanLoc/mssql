using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E03;

// Worked solutions for E03, each followed by a Why note. Run: dotnet test --filter E03Solutions
public class E03Solutions : E03Checks
{
    // Exercise 1 — Territory scoreboard
    // For orders with from <= OrderDate < to, return one TerritorySales row per territory:
    // territory Name, number of orders, number of distinct customers, and sum of TotalDue;
    // ordered by Total descending.
    // Rules: one round trip; grouped and counted in SQL (GROUP BY, COUNT(DISTINCT ...)).
    protected override Task<List<TerritorySales>> Ex1(AdventureWorksContext db, DateTime from, DateTime to)
        => db.SalesOrderHeaders
            .Where(h => h.OrderDate >= from && h.OrderDate < to)
            .GroupBy(h => h.Territory!.Name)
            .Select(g => new
            {
                Territory = g.Key,
                Orders = g.Count(),
                Customers = g.Select(h => h.CustomerId).Distinct().Count(),
                Total = g.Sum(h => h.TotalDue),
            })
            .OrderByDescending(x => x.Total)
            .Select(x => new TerritorySales(x.Territory, x.Orders, x.Customers, x.Total))
            .ToListAsync();
    // Why: grouping by a navigation property makes EF join SalesTerritory and GROUP BY its Name. Inside the
    // group, Select(...).Distinct().Count() becomes COUNT(DISTINCT CustomerID). The half-open date range
    // (>= from, < to) is the pattern from T-SQL lesson 02; E06 shows why it beats OrderDate.Year == 2013.
    // Tempting-but-wrong: loading the year's orders and grouping them in memory, which ships ~14,000 rows
    // to produce 10.

    // Exercise 2 — Loyal customers
    // Return customers with at least `minOrders` orders as CustomerOrderMix: CustomerId, Total
    // orders, Online orders (OnlineOrderFlag) and Offline orders; ordered by Total descending,
    // then CustomerId.
    // Rules: one round trip; the minimum is applied in SQL (HAVING).
    protected override Task<List<CustomerOrderMix>> Ex2(AdventureWorksContext db, int minOrders)
        => db.SalesOrderHeaders
            .GroupBy(h => h.CustomerId)
            .Where(g => g.Count() >= minOrders)
            .Select(g => new
            {
                CustomerId = g.Key,
                Total = g.Count(),
                Online = g.Count(h => h.OnlineOrderFlag),
                Offline = g.Count(h => !h.OnlineOrderFlag),
            })
            .OrderByDescending(x => x.Total).ThenBy(x => x.CustomerId)
            .Select(x => new CustomerOrderMix(x.CustomerId, x.Total, x.Online, x.Offline))
            .ToListAsync();
    // Why: a Where between GroupBy and Select filters groups, so it becomes HAVING COUNT(*) >= @minOrders.
    // Count(predicate) becomes COUNT(CASE WHEN ... THEN 1 END): three counts in one pass over the table.
    // Tempting-but-wrong: Where(g => ...) after the Select works too, but filtering the groups before
    // projecting says what you mean and gives the same HAVING.

    // Exercise 3 — Never ordered
    // Return finished-goods products (FinishedGoodsFlag) that appear on no SalesOrderDetail,
    // ordered by ProductId. Note: Product has no navigation to SalesOrderDetail.
    // Rules: one round trip; the SQL uses EXISTS, never COUNT.
    protected override Task<List<ProductRef>> Ex3(AdventureWorksContext db)
        => db.Products
            .Where(p => p.FinishedGoodsFlag && !db.SalesOrderDetails.Any(d => d.ProductId == p.ProductId))
            .OrderBy(p => p.ProductId)
            .Select(p => new ProductRef(p.ProductId, p.Name))
            .ToListAsync();
    // Why: with no navigation, reference the other DbSet inside the lambda. EF turns !Any(...) into a
    // correlated NOT EXISTS, which stops probing a product at its first order line.
    // Tempting-but-wrong: db.SalesOrderDetails.Count(d => d.ProductId == p.ProductId) == 0 counts every line
    // just to compare with zero. Loading the ordered IDs first and using Contains is an extra round trip.

    // Exercise 4 — One plan for every list
    // Return the products whose ProductId is in `productIds`, ordered by ProductId. The check calls
    // this with lists of 3, 7 and 12 IDs, and all three calls must send the same SQL text, so they
    // share one cached plan (EF 10's default pads lists to different sizes).
    // Rules: one round trip per call; the same SQL for every list size.
    protected override Task<List<ProductRef>> Ex4(AdventureWorksContext db, List<int> productIds)
        => db.Products
            .Where(p => EF.Parameter(productIds).Contains(p.ProductId))
            .OrderBy(p => p.ProductId)
            .Select(p => new ProductRef(p.ProductId, p.Name))
            .ToListAsync();
    // Why: EF.Parameter sends the whole list as one JSON string and reads it with OPENJSON, so the SQL text
    // never depends on the list's length. The trade-off is that the optimizer can't see the values and has
    // to guess the row count. The default (3 parameters, then 10, then 20) gives it more to work with, at
    // the price of one plan per bucket.
    // Tempting-but-wrong: EF.Constant(productIds) inlines the IDs, which means one SQL text and one plan
    // per distinct list.

    // Exercise 5 — Top three per territory
    // For orders with from <= OrderDate < to, return each territory's 3 largest orders by TotalDue
    // (ties broken by SalesOrderId), ordered by Territory, then TotalDue descending, then SalesOrderId.
    // Rules: one round trip.
    protected override Task<List<TerritoryTopOrder>> Ex5(AdventureWorksContext db, DateTime from, DateTime to)
        => db.SalesTerritories
            .SelectMany(t => t.SalesOrderHeaders
                .Where(h => h.OrderDate >= from && h.OrderDate < to)
                .OrderByDescending(h => h.TotalDue).ThenBy(h => h.SalesOrderId)
                .Take(3)
                .Select(h => new { Territory = t.Name, h.SalesOrderId, h.TotalDue }))
            .OrderBy(x => x.Territory).ThenByDescending(x => x.TotalDue).ThenBy(x => x.SalesOrderId)
            .Select(x => new TerritoryTopOrder(x.Territory, x.SalesOrderId, x.TotalDue))
            .ToListAsync();
    // Why: SelectMany over the territory's orders with Take(3) becomes CROSS APPLY (SELECT TOP(3) ...), the
    // APPLY pattern from T-SQL lesson 18, in one round trip. Grouping orders by TerritoryId and taking 3 per
    // group is equally valid; EF translates that shape with ROW_NUMBER() instead.
    // Tempting-but-wrong: a loop that queries each territory's top 3 is N+1 round trips.

    // Challenge 1 — Running total
    // Return customer `customerId`'s orders ordered by OrderDate, then SalesOrderId, each with the
    // running total of TotalDue up to and including that order.
    // Rules: one round trip. (LINQ has no SUM() OVER — see concept 6.)
    protected override async Task<List<OrderRunningTotal>> Ch1(AdventureWorksContext db, int customerId)
    {
        var orders = await db.SalesOrderHeaders
            .Where(h => h.CustomerId == customerId)
            .OrderBy(h => h.OrderDate).ThenBy(h => h.SalesOrderId)
            .Select(h => new { h.SalesOrderId, h.OrderDate, h.TotalDue })
            .ToListAsync();

        var running = 0m;
        return orders
            .Select(o => new OrderRunningTotal(o.SalesOrderId, o.OrderDate, o.TotalDue, running += o.TotalDue))
            .ToList();
    }
    // Why: SQL does the filtering and ordering, and C# finishes the running sum in one pass. That's fine
    // for 28 rows. A correlated subquery (sum of every earlier order, for each order) also works in one
    // round trip, but it's O(n²) on the server. For big sets, write the window function in raw SQL (E04
    // composes LINQ on top of it).

    // Challenge 2 — ROLLUP without ROLLUP
    // For orders with from <= OrderDate < to, return what GROUP BY ROLLUP(Territory.Group,
    // Territory.Name) would return: one row per (Group, Territory) with the total TotalDue, one
    // subtotal row per Group (Territory = null), and one grand-total row (both null). Any order.
    // Rules: one round trip; LINQ only (no raw SQL).
    protected override Task<List<RollupRow>> Ch2(AdventureWorksContext db, DateTime from, DateTime to)
    {
        var orders = db.SalesOrderHeaders.Where(h => h.OrderDate >= from && h.OrderDate < to);

        var perTerritory = orders
            .GroupBy(h => new { h.Territory!.Group, h.Territory.Name })
            .Select(g => new { Group = (string?)g.Key.Group, Territory = (string?)g.Key.Name, Total = g.Sum(h => h.TotalDue) });
        var perGroup = orders
            .GroupBy(h => h.Territory!.Group)
            .Select(g => new { Group = (string?)g.Key, Territory = (string?)null, Total = g.Sum(h => h.TotalDue) });
        var grandTotal = orders
            .GroupBy(h => 1)
            .Select(g => new { Group = (string?)null, Territory = (string?)null, Total = g.Sum(h => h.TotalDue) });

        return perTerritory.Concat(perGroup).Concat(grandTotal)
            .Select(x => new RollupRow(x.Group, x.Territory, x.Total))
            .ToListAsync();
    }
    // Why: ROLLUP(a, b) is GROUP BY (a, b), then GROUP BY a, then the grand total, stacked with UNION ALL.
    // Concat sends all three as one statement. The three shapes must match exactly, hence the (string?)
    // casts on the null columns. Real ROLLUP reads the table once, while this reads it three times, so it's
    // fine at this size and a reason to reach for raw SQL (E04) at scale.

    // Challenge 3 — Most expensive, ties included
    // For every product subcategory, return the products whose ListPrice equals the highest ListPrice
    // in that subcategory. All tied products, not just one. Skip products without a subcategory.
    // Order by ProductSubcategoryId, then ProductId.
    // Rules: one round trip.
    protected override Task<List<SubcategoryTopPrice>> Ch3(AdventureWorksContext db)
        => db.Products
            .Where(p => p.ProductSubcategory != null
                && p.ListPrice == p.ProductSubcategory.Products.Max(x => x.ListPrice))
            .OrderBy(p => p.ProductSubcategoryId).ThenBy(p => p.ProductId)
            .Select(p => new SubcategoryTopPrice(p.ProductSubcategoryId!.Value, p.ProductId, p.ListPrice))
            .ToListAsync();
    // Why: comparing each product with its subcategory's MAX (a correlated subquery) keeps every product at
    // the top price. That's what RANK() = 1 does in T-SQL lesson 07.
    // Tempting-but-wrong: GroupBy(p => p.ProductSubcategoryId)
    //     .Select(g => g.OrderByDescending(p => p.ListPrice).Select(p => new { ... }).First()).
    // It translates (ROW_NUMBER() ... <= 1) but keeps one product per subcategory and silently drops the
    // ties: 37 rows instead of 94.
}
