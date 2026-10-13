using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E13;

// Your context for E13. Exercises 1-5 and challenge 3 don't need anything here. Challenge 1 asks you
// to map VendorMonth in MapVendorMonths; until you do, VendorMonth isn't part of the model.
public class E13ExerciseContext(DbContextOptions<AdventureWorksContext> options) : E13Context(options)
{
    protected override void MapVendorMonths(ModelBuilder modelBuilder)
    {
    }
}

// E13 exercises: the capstone. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E13Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
// `db` is your E13ExerciseContext: everything in AdventureWorksContext plus VendorMonths (challenge 1).
public class E13Exercises : E13Checks<E13ExerciseContext>
{
    // Exercise 1 — Products that beat the average
    // Return each product whose total sales (sum of SalesOrderDetail.LineTotal) beat the average
    // product's total sales, as ProductTotal, highest first (ties by Name).
    // Rules: one round trip; the average is computed in SQL.
    protected override Task<List<ProductTotal>> Ex1(E13ExerciseContext db)
        => throw new NotImplementedException();

    // Exercise 2 — Average monthly revenue
    // For each territory, the average of its monthly revenue (the sum of TotalDue per calendar month),
    // as TerritoryRevenue, highest first.
    // Rules: one round trip.
    protected override Task<List<TerritoryRevenue>> Ex2(E13ExerciseContext db)
        => throw new NotImplementedException();

    // Exercise 3 — Best customers per territory
    // For each territory, the `top` customers by lifetime spend (sum of TotalDue of their orders in that
    // territory), as TopCustomer, ordered by Territory, then spend (highest first), then CustomerId.
    // Rules: one round trip; the top-N cut happens in SQL.
    protected override Task<List<TopCustomer>> Ex3(E13ExerciseContext db, int top)
        => throw new NotImplementedException();

    // Exercise 4 — Orders per year, side by side
    // One TerritoryOrders row per territory: its order counts for 2012, 2013 and 2014 plus its total
    // across all years (2011 included), ordered by Territory. No PIVOT, no one-query-per-year.
    // Rules: one round trip.
    protected override Task<List<TerritoryOrders>> Ex4(E13ExerciseContext db)
        => throw new NotImplementedException();

    // Exercise 5 — Latest orders per salesperson
    // For every salesperson in Sales.SalesPerson, their `top` most recent orders (by OrderDate, ties by
    // the higher SalesOrderId), as RecentOrder, ordered by SalesPersonId, then OrderDate and SalesOrderId
    // (both newest first). A salesperson with no orders still appears once, with null order columns.
    // Rules: one round trip; the top-N cut happens in SQL.
    protected override Task<List<RecentOrder>> Ex5(E13ExerciseContext db, int top)
        => throw new NotImplementedException();

    // Challenge 1 — Where LINQ runs out: month-over-month growth
    // In E13ExerciseContext.MapVendorMonths, map VendorMonth (E13Context.cs) as a keyless entity over a
    // SQL query: one row per vendor and calendar month (first day of the month) of purchases (sum of
    // PurchaseOrderDetail.LineTotal), with the previous month's total from LAG. Then return vendor
    // `vendorId`'s months from db.VendorMonths as VendorGrowth, ordered by OrderMonth, where GrowthPct is
    // 100 * (MonthTotal - PrevMonthTotal) / PrevMonthTotal rounded to 1 decimal (null for the first month).
    // Rules: one round trip; the vendor filter, the growth and the ordering are LINQ on top of your query.
    protected override Task<List<VendorGrowth>> Ch1(E13ExerciseContext db, int vendorId)
        => throw new NotImplementedException();

    // Challenge 2 — The final boss: the vendor report
    // For vendor `vendorId`, lesson 18's purchasing report: for each month, the month total and growth %
    // (as in challenge 1), and the month's top 2 products by line total (ties by product Name), each with
    // its total and its contribution % to the month total (rounded to 1 decimal). One VendorReportRow per
    // month and product, ordered by OrderMonth, then ProductTotal (highest first), then ProductName.
    // Rules: one round trip; build on db.VendorMonths.
    protected override Task<List<VendorReportRow>> Ch2(E13ExerciseContext db, int vendorId)
        => throw new NotImplementedException();

    // Challenge 3 — Place an order
    // Place `request` as a new order: take Quantity units of the product from ProductInventory at
    // LocationId, then save a SalesOrderHeader (the customer, AddressId as both bill-to and ship-to,
    // ShipMethodId, OrderDate now, DueDate a week later) with one SalesOrderDetail (SpecialOfferId 1, the
    // product's ListPrice as UnitPrice). Return the new SalesOrderId, or null when the location has fewer
    // than Quantity units: then nothing changes. If saving the order fails, the stock must be put back and
    // the exception must reach the caller.
    // Rules: checking and taking the stock is ONE statement (no read-then-write race between two buyers);
    // all-or-nothing; db already has a transaction open (the test's), so use a savepoint.
    protected override Task<int?> Ch3(E13ExerciseContext db, OrderRequest request)
        => throw new NotImplementedException();
}
