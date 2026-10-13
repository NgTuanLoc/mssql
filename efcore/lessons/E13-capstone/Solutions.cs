using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCoreLessons.E13;

// The solution's context: VendorMonth mapped to a SQL query (challenge 1).
public class E13SolutionContext(DbContextOptions<AdventureWorksContext> options) : E13Context(options)
{
    protected override void MapVendorMonths(ModelBuilder modelBuilder)
        => modelBuilder.Entity<VendorMonth>().HasNoKey().ToSqlQuery("""
            SELECT VendorID AS VendorId, OrderMonth, MonthTotal,
                   LAG(MonthTotal) OVER (PARTITION BY VendorID ORDER BY OrderMonth) AS PrevMonthTotal
            FROM (
                SELECT poh.VendorID, DATEFROMPARTS(YEAR(poh.OrderDate), MONTH(poh.OrderDate), 1) AS OrderMonth,
                       SUM(pod.LineTotal) AS MonthTotal
                FROM Purchasing.PurchaseOrderHeader AS poh
                JOIN Purchasing.PurchaseOrderDetail AS pod ON pod.PurchaseOrderID = poh.PurchaseOrderID
                GROUP BY poh.VendorID, DATEFROMPARTS(YEAR(poh.OrderDate), MONTH(poh.OrderDate), 1)
            ) AS m
            """);
    // Why: ToSqlQuery makes VendorMonth a keyless entity whose "table" is this query: LINQ composes over
    // it as a subquery, like FromSql (E04), but it's named, mapped once and reusable everywhere. LAG has
    // no LINQ form (E03 concept 6), so it lives here. Partitioning by VendorID keeps each vendor's
    // previous month its own, so a filter composed on top is still correct. No ORDER BY and no CTE:
    // composed SQL can't contain either (E04 concept 2).
}

// Worked solutions for E13, each followed by a Why note. Run: dotnet test --filter E13Solutions
public class E13Solutions : E13Checks<E13SolutionContext>
{
    // Exercise 1 — Products that beat the average
    // Return each product whose total sales (sum of SalesOrderDetail.LineTotal) beat the average
    // product's total sales, as ProductTotal, highest first (ties by Name).
    // Rules: one round trip; the average is computed in SQL.
    protected override Task<List<ProductTotal>> Ex1(E13SolutionContext db)
    {
        var totals = db.SalesOrderDetails
            .GroupBy(d => d.ProductId)
            .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(d => d.LineTotal) });

        return totals
            .Where(t => t.TotalSold > totals.Average(x => x.TotalSold))
            .Join(db.Products, t => t.ProductId, p => p.ProductId, (t, p) => new { p.Name, t.TotalSold })
            .OrderByDescending(x => x.TotalSold).ThenBy(x => x.Name)
            .Select(x => new ProductTotal(x.Name, x.TotalSold))
            .ToListAsync();
    }
    // Why: `totals` is the ProductTotals CTE from lesson 18: an IQueryable is a query definition, not
    // data, so naming it costs nothing and using it twice inlines it twice, exactly like referencing a CTE
    // twice. Nothing runs until ToListAsync, so the whole pipeline is one SELECT with the average as a
    // subquery. Calling ToListAsync on `totals` first would pull every product total into memory.

    // Exercise 2 — Average monthly revenue
    // For each territory, the average of its monthly revenue (the sum of TotalDue per calendar month),
    // as TerritoryRevenue, highest first.
    // Rules: one round trip.
    protected override Task<List<TerritoryRevenue>> Ex2(E13SolutionContext db)
        => db.SalesOrderHeaders
            .GroupBy(h => new { h.TerritoryId, h.OrderDate.Year, h.OrderDate.Month })
            .Select(g => new { g.Key.TerritoryId, Revenue = g.Sum(h => h.TotalDue) })
            .Join(db.SalesTerritories, m => m.TerritoryId, t => (int?)t.TerritoryId, (m, t) => new { t.Name, m.Revenue })
            .GroupBy(x => x.Name)
            .Select(g => new { Territory = g.Key, AvgMonthlyRevenue = g.Average(x => x.Revenue) })
            .OrderByDescending(x => x.AvgMonthlyRevenue)
            .Select(x => new TerritoryRevenue(x.Territory, x.AvgMonthlyRevenue))
            .ToListAsync();
    // Why: two grain changes, two GroupBys: orders to territory-months, then territory-months to
    // territories. EF nests the first as a derived table, the same shape as lesson 18's MonthlyRevenue
    // CTE. Grouping by Year and Month (not a DateTime built from them) keeps the key simple to translate.

    // Exercise 3 — Best customers per territory
    // For each territory, the `top` customers by lifetime spend (sum of TotalDue of their orders in that
    // territory), as TopCustomer, ordered by Territory, then spend (highest first), then CustomerId.
    // Rules: one round trip; the top-N cut happens in SQL.
    protected override Task<List<TopCustomer>> Ex3(E13SolutionContext db, int top)
    {
        var spend = db.SalesOrderHeaders
            .GroupBy(h => new { h.TerritoryId, h.CustomerId })
            .Select(g => new { g.Key.TerritoryId, g.Key.CustomerId, LifetimeSpend = g.Sum(h => h.TotalDue) });

        return db.SalesTerritories
            .SelectMany(t => spend
                .Where(s => s.TerritoryId == t.TerritoryId)
                .OrderByDescending(s => s.LifetimeSpend).ThenBy(s => s.CustomerId)
                .Take(top)
                .Select(s => new { Territory = t.Name, s.CustomerId, s.LifetimeSpend }))
            .OrderBy(x => x.Territory).ThenByDescending(x => x.LifetimeSpend).ThenBy(x => x.CustomerId)
            .Select(x => new TopCustomer(x.Territory, x.CustomerId, x.LifetimeSpend))
            .ToListAsync();
    }
    // Why: the per-customer totals are a grouped query, and a correlated Take over it per territory is
    // lesson 18's top-N-per-group: EF emits CROSS APPLY (SELECT TOP(@top) ...). Other shapes, such as
    // GroupBy(...).Select(g => g.OrderBy(...).Take(top)), come out as ROW_NUMBER() <= @top. Either way the
    // cut is in SQL. The CustomerId tiebreak makes "the top 3" well defined when two spends are equal.

    // Exercise 4 — Orders per year, side by side
    // One TerritoryOrders row per territory: its order counts for 2012, 2013 and 2014 plus its total,
    // ordered by Territory. No PIVOT, no one-query-per-year.
    // Rules: one round trip.
    protected override Task<List<TerritoryOrders>> Ex4(E13SolutionContext db)
        => db.SalesOrderHeaders
            .Where(h => h.TerritoryId != null)
            .GroupBy(h => h.Territory!.Name)
            .Select(g => new
            {
                Territory = g.Key,
                Orders2012 = g.Count(h => h.OrderDate.Year == 2012),
                Orders2013 = g.Count(h => h.OrderDate.Year == 2013),
                Orders2014 = g.Count(h => h.OrderDate.Year == 2014),
                TotalOrders = g.Count(),
            })
            .OrderBy(x => x.Territory)
            .Select(x => new TerritoryOrders(x.Territory, x.Orders2012, x.Orders2013, x.Orders2014, x.TotalOrders))
            .ToListAsync();
    // Why: Count(predicate) inside a group becomes COUNT(CASE WHEN ... THEN 1 END), lesson 18's
    // conditional aggregation, so the orders are read once. Grouping by the navigation's Name is the join.
    // The aggregates go into an anonymous type first: EF can't translate through a positional record's
    // constructor, so the record is the last step.

    // Exercise 5 — Latest orders per salesperson
    // For every salesperson in Sales.SalesPerson, their `top` most recent orders (by OrderDate, ties by
    // the higher SalesOrderId), as RecentOrder, ordered by SalesPersonId, then OrderDate and SalesOrderId
    // (both newest first). A salesperson with no orders still appears once, with null order columns.
    // Rules: one round trip.
    protected override Task<List<RecentOrder>> Ex5(E13SolutionContext db, int top)
        => db.SalesPeople
            .SelectMany(
                sp => sp.SalesOrderHeaders
                    .OrderByDescending(h => h.OrderDate).ThenByDescending(h => h.SalesOrderId)
                    .Take(top)
                    .DefaultIfEmpty(),
                (sp, h) => new { sp.BusinessEntityId, SalesOrderId = (int?)h!.SalesOrderId, OrderDate = (DateTime?)h.OrderDate, TotalDue = (decimal?)h.TotalDue })
            .OrderBy(x => x.BusinessEntityId).ThenByDescending(x => x.OrderDate).ThenByDescending(x => x.SalesOrderId)
            .Select(x => new RecentOrder(x.BusinessEntityId, x.SalesOrderId, x.OrderDate, x.TotalDue))
            .ToListAsync();
    // Why: SelectMany + Take per salesperson is lesson 18's APPLY. DefaultIfEmpty is what keeps salespeople
    // with no orders (lesson 18 exercise 4): here EF emits a LEFT JOIN over ROW_NUMBER() <= @top, which
    // returns the same rows as OUTER APPLY. Without DefaultIfEmpty the join is an inner one and they
    // silently disappear. The casts to nullable types tell EF the columns can be NULL for those rows.

    // Challenge 1 — Where LINQ runs out: month-over-month growth
    // In E13ExerciseContext.MapVendorMonths, map VendorMonth (E13Context.cs) as a keyless entity over a
    // SQL query: one row per vendor and calendar month (first day of the month) of purchases (sum of
    // PurchaseOrderDetail.LineTotal), with the previous month's total from LAG. Then return vendor
    // `vendorId`'s months from db.VendorMonths as VendorGrowth, ordered by OrderMonth, where GrowthPct is
    // 100 * (MonthTotal - PrevMonthTotal) / PrevMonthTotal rounded to 1 decimal (null for the first month).
    // Rules: one round trip; the vendor filter, the growth and the ordering are LINQ on top of your query.
    protected override Task<List<VendorGrowth>> Ch1(E13SolutionContext db, int vendorId)
        => db.VendorMonths
            .Where(m => m.VendorId == vendorId)
            .OrderBy(m => m.OrderMonth)
            .Select(m => new VendorGrowth(
                m.OrderMonth,
                m.MonthTotal,
                m.PrevMonthTotal == null || m.PrevMonthTotal == 0
                    ? null
                    : Math.Round(100 * (m.MonthTotal - m.PrevMonthTotal.Value) / m.PrevMonthTotal.Value, 1)))
            .ToListAsync();
    // Why: LINQ has no LAG, so that one step is SQL, mapped in the context (see E13SolutionContext). The
    // rest is ordinary LINQ composed over it: the WHERE, the ORDER BY and the ROUND arithmetic all run in
    // SQL Server. Unlike FromSql, the mapping is written once and every query can use db.VendorMonths.

    // Challenge 2 — The final boss: the vendor report
    // For vendor `vendorId`, lesson 18's purchasing report: for each month, the month total and growth %
    // (as in challenge 1), and the month's top 2 products by line total (ties by product Name), each with
    // its total and its contribution % to the month total (rounded to 1 decimal). One VendorReportRow per
    // month and product, ordered by OrderMonth, then ProductTotal (highest first), then ProductName.
    // Rules: one round trip; build on db.VendorMonths.
    protected override Task<List<VendorReportRow>> Ch2(E13SolutionContext db, int vendorId)
    {
        var lines = db.PurchaseOrderDetails.Where(d => d.PurchaseOrder.VendorId == vendorId);

        return db.VendorMonths
            .Where(m => m.VendorId == vendorId)
            .SelectMany(m => lines
                .Where(d => d.PurchaseOrder.OrderDate >= m.OrderMonth && d.PurchaseOrder.OrderDate < m.OrderMonth.AddMonths(1))
                .GroupBy(d => d.Product.Name)
                .Select(g => new { ProductName = g.Key, ProductTotal = g.Sum(d => d.LineTotal) })
                .OrderByDescending(p => p.ProductTotal).ThenBy(p => p.ProductName)
                .Take(2)
                .Select(p => new { Month = m, p.ProductName, p.ProductTotal }))
            .OrderBy(x => x.Month.OrderMonth).ThenByDescending(x => x.ProductTotal).ThenBy(x => x.ProductName)
            .Select(x => new VendorReportRow(
                x.Month.OrderMonth,
                x.Month.MonthTotal,
                x.Month.PrevMonthTotal == null || x.Month.PrevMonthTotal == 0
                    ? null
                    : Math.Round(100 * (x.Month.MonthTotal - x.Month.PrevMonthTotal.Value) / x.Month.PrevMonthTotal.Value, 1),
                x.ProductName,
                x.ProductTotal,
                Math.Round(100 * x.ProductTotal / x.Month.MonthTotal, 1)))
            .ToListAsync();
    }
    // Why: the same pipeline as lesson 18, with each step a different tool: the mapped query does what
    // LINQ can't (LAG), a correlated SelectMany + Take does the per-month top 2 (CROSS APPLY), and the
    // arithmetic is translated LINQ. The month range (>= first day, < first day of the next month) is
    // SARGable, unlike comparing YEAR() and MONTH() of each order (E06).

    // Challenge 3 — Place an order
    // Place `request` as a new order: take Quantity units of the product from ProductInventory at
    // LocationId, then save a SalesOrderHeader (the customer, AddressId as both bill-to and ship-to,
    // ShipMethodId, OrderDate now, DueDate a week later) with one SalesOrderDetail (SpecialOfferId 1, the
    // product's ListPrice as UnitPrice). Return the new SalesOrderId, or null when the location has fewer
    // than Quantity units: then nothing changes. If saving the order fails, the stock must be put back and
    // the exception must reach the caller.
    // Rules: checking and taking the stock is ONE statement (no read-then-write race between two buyers);
    // all-or-nothing; db already has a transaction open (the test's), so use a savepoint.
    protected override async Task<int?> Ch3(E13SolutionContext db, OrderRequest request)
    {
        var transaction = db.Database.CurrentTransaction!;
        await transaction.CreateSavepointAsync("PlaceOrder");
        try
        {
            var taken = await db.ProductInventories
                .Where(i => i.ProductId == request.ProductId && i.LocationId == request.LocationId && i.Quantity >= request.Quantity)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.Quantity, i => (short)(i.Quantity - request.Quantity)));
            if (taken == 0) return null;

            var price = await db.Products.Where(p => p.ProductId == request.ProductId).Select(p => p.ListPrice).SingleAsync();
            var order = new SalesOrderHeader
            {
                CustomerId = request.CustomerId,
                BillToAddressId = request.AddressId,
                ShipToAddressId = request.AddressId,
                ShipMethodId = request.ShipMethodId,
                OrderDate = DateTime.Now,
                DueDate = DateTime.Now.AddDays(7),
            };
            order.SalesOrderDetails.Add(new SalesOrderDetail
            {
                ProductId = request.ProductId,
                SpecialOfferId = 1,
                OrderQty = request.Quantity,
                UnitPrice = price,
            });
            db.SalesOrderHeaders.Add(order);
            await db.SaveChangesAsync();
            return order.SalesOrderId;
        }
        catch
        {
            await transaction.RollbackToSavepointAsync("PlaceOrder");
            throw;
        }
    }
    // Why: the WHERE Quantity >= @qty inside the UPDATE makes check-and-take atomic: two buyers can't both
    // see 100 units and both take them, because the second UPDATE re-checks the row the first one changed
    // and matches nothing. Its row count is the answer. SaveChanges already undoes its own batch when it
    // fails, but not the stock UPDATE that ran before it; the savepoint covers both. In an app without an
    // outer transaction, BeginTransaction + Commit plays the savepoint's role (E09).
}
