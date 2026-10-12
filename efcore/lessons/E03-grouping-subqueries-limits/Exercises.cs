using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E03;

// E03 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E03Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
public class E03Exercises : E03Checks
{
    // Exercise 1 — Territory scoreboard
    // For orders with from <= OrderDate < to, return one TerritorySales row per territory:
    // territory Name, number of orders, number of distinct customers, and sum of TotalDue;
    // ordered by Total descending.
    // Rules: one round trip; grouped and counted in SQL (GROUP BY, COUNT(DISTINCT ...)).
    protected override Task<List<TerritorySales>> Ex1(AdventureWorksContext db, DateTime from, DateTime to)
        => throw new NotImplementedException();

    // Exercise 2 — Loyal customers
    // Return customers with at least `minOrders` orders as CustomerOrderMix: CustomerId, Total
    // orders, Online orders (OnlineOrderFlag) and Offline orders; ordered by Total descending,
    // then CustomerId.
    // Rules: one round trip; the minimum is applied in SQL (HAVING).
    protected override Task<List<CustomerOrderMix>> Ex2(AdventureWorksContext db, int minOrders)
        => throw new NotImplementedException();

    // Exercise 3 — Never ordered
    // Return finished-goods products (FinishedGoodsFlag) that appear on no SalesOrderDetail,
    // ordered by ProductId. Note: Product has no navigation to SalesOrderDetail.
    // Rules: one round trip; the SQL uses EXISTS, never COUNT.
    protected override Task<List<ProductRef>> Ex3(AdventureWorksContext db)
        => throw new NotImplementedException();

    // Exercise 4 — One plan for every list
    // Return the products whose ProductId is in `productIds`, ordered by ProductId. The check calls
    // this with lists of 3, 7 and 12 IDs, and all three calls must send the same SQL text, so they
    // share one cached plan (EF 10's default pads lists to different sizes).
    // Rules: one round trip per call; the same SQL for every list size.
    protected override Task<List<ProductRef>> Ex4(AdventureWorksContext db, List<int> productIds)
        => throw new NotImplementedException();

    // Exercise 5 — Top three per territory
    // For orders with from <= OrderDate < to, return each territory's 3 largest orders by TotalDue
    // (ties broken by SalesOrderId), ordered by Territory, then TotalDue descending, then SalesOrderId.
    // Rules: one round trip.
    protected override Task<List<TerritoryTopOrder>> Ex5(AdventureWorksContext db, DateTime from, DateTime to)
        => throw new NotImplementedException();

    // Challenge 1 — Running total
    // Return customer `customerId`'s orders ordered by OrderDate, then SalesOrderId, each with the
    // running total of TotalDue up to and including that order.
    // Rules: one round trip. (LINQ has no SUM() OVER — see concept 6.)
    protected override Task<List<OrderRunningTotal>> Ch1(AdventureWorksContext db, int customerId)
        => throw new NotImplementedException();

    // Challenge 2 — ROLLUP without ROLLUP
    // For orders with from <= OrderDate < to, return what GROUP BY ROLLUP(Territory.Group,
    // Territory.Name) would return: one row per (Group, Territory) with the total TotalDue, one
    // subtotal row per Group (Territory = null), and one grand-total row (both null). Any order.
    // Rules: one round trip; LINQ only (no raw SQL).
    protected override Task<List<RollupRow>> Ch2(AdventureWorksContext db, DateTime from, DateTime to)
        => throw new NotImplementedException();

    // Challenge 3 — Most expensive, ties included
    // For every product subcategory, return the products whose ListPrice equals the highest ListPrice
    // in that subcategory. All tied products, not just one. Skip products without a subcategory.
    // Order by ProductSubcategoryId, then ProductId.
    // Rules: one round trip.
    protected override Task<List<SubcategoryTopPrice>> Ch3(AdventureWorksContext db)
        => throw new NotImplementedException();
}
