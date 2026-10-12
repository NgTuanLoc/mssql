using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E05;

// E05 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E05Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E05Exercises : E05Checks
{
    // Exercise 1 — Orders with their lines
    // Return the orders of customer `customerId`, ordered by SalesOrderId, each with its
    // SalesOrderDetails collection loaded.
    // Rules: one round trip in total, including anything the check reads afterwards.
    protected override Task<List<SalesOrderHeader>> Ex1(E05Context db, int customerId)
        => throw new NotImplementedException();

    // Exercise 2 — Only the recent orders
    // Return customer `customerId` with only the orders placed on or after `since` loaded into
    // SalesOrderHeaders.
    // Rules: one round trip.
    protected override Task<Customer> Ex2(E05Context db, int customerId, DateTime since)
        => throw new NotImplementedException();

    // Exercise 3 — Load the reviews later
    // `product` is already tracked by `db`, and lazy loading is switched off for this exercise.
    // Load its ProductReviews collection.
    // Rules: one round trip; don't query the product again.
    protected override Task Ex3(E05Context db, Product product)
        => throw new NotImplementedException();

    // Exercise 4 — Summaries instead of graphs
    // Return one OrderSummary per order placed on `orderDate`, ordered by SalesOrderId: the order's
    // SalesOrderId, the customer's AccountNumber, its number of lines, and its TotalDue.
    // Rules: one round trip; nothing tracked; don't load the lines (count them in SQL).
    protected override Task<List<OrderSummary>> Ex4(E05Context db, DateTime orderDate)
        => throw new NotImplementedException();

    // Exercise 5 — No N+1
    // Return every territory's Name and its number of customers, ordered by territory name.
    // Rules: one round trip, however many territories there are.
    protected override Task<List<TerritoryCustomers>> Ex5(E05Context db)
        => throw new NotImplementedException();

    // Challenge 1 — No cartesian explosion
    // Return sales person `salesPersonId` with SalesOrderHeaders, Stores and SalesPersonQuotaHistories
    // all loaded. Loading them in one query would return orders × stores × quotas rows
    // (473 × 76 × 12 = 431,376 for sales person 277).
    // Rules: no query joins two collections to the parent; at most 4 round trips.
    protected override Task<SalesPerson> Ch1(E05Context db, int salesPersonId)
        => throw new NotImplementedException();

    // Challenge 2 — The three most recent orders each
    // Return the customers with at least `minOrders` orders, ordered by CustomerId, each with only its
    // 3 most recent orders loaded (OrderDate descending, then SalesOrderId descending), in that order.
    // Rules: one round trip.
    protected override Task<List<Customer>> Ch2(E05Context db, int minOrders)
        => throw new NotImplementedException();

    // Challenge 3 — Load part, count the rest
    // `salesPerson` is already tracked by `db`. Load only its orders placed in calendar year `year` into
    // SalesOrderHeaders, and return its number of stores without loading them.
    // Rules: exactly two round trips; the Stores collection must stay unloaded.
    protected override Task<int> Ch3(E05Context db, SalesPerson salesPerson, int year)
        => throw new NotImplementedException();
}
