using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E05;

// Worked solutions for E05, each followed by a Why note. Run: dotnet test --filter E05Solutions
public class E05Solutions : E05Checks
{
    // Exercise 1 — Orders with their lines
    // Return the orders of customer `customerId`, ordered by SalesOrderId, each with its
    // SalesOrderDetails collection loaded.
    // Rules: one round trip in total, including anything the check reads afterwards.
    protected override Task<List<SalesOrderHeader>> Ex1(E05Context db, int customerId)
        => db.SalesOrderHeaders
            .Where(h => h.CustomerId == customerId)
            .OrderBy(h => h.SalesOrderId)
            .Include(h => h.SalesOrderDetails)
            .ToListAsync();
    // Why: Include adds the lines to the same query as a LEFT JOIN, ordered so EF can stitch each line to
    // its order, and marks every collection as loaded. Tempting-but-wrong: skip the Include and let lazy
    // loading fetch each order's lines when the check counts them: 1 + 28 round trips (the N+1 problem).

    // Exercise 2 — Only the recent orders
    // Return customer `customerId` with only the orders placed on or after `since` loaded into
    // SalesOrderHeaders.
    // Rules: one round trip.
    protected override Task<Customer> Ex2(E05Context db, int customerId, DateTime since)
        => db.Customers
            .Include(c => c.SalesOrderHeaders.Where(h => h.OrderDate >= since))
            .SingleAsync(c => c.CustomerId == customerId);
    // Why: a filtered Include puts the Where inside the join, so only matching orders come back, and EF
    // still marks the collection as loaded. Tempting-but-wrong: Include everything and filter the
    // collection in C#: every order crosses the wire, and the entity's collection still holds all of them.

    // Exercise 3 — Load the reviews later
    // `product` is already tracked by `db`, and lazy loading is switched off for this exercise.
    // Load its ProductReviews collection.
    // Rules: one round trip; don't query the product again.
    protected override Task Ex3(E05Context db, Product product)
        => db.Entry(product).Collection(p => p.ProductReviews).LoadAsync();
    // Why: explicit loading runs one query for exactly one navigation of an entity you already have, and
    // marks it loaded. It's the controlled version of lazy loading: you decide when the round trip
    // happens. Tempting-but-wrong: query the product again with Include. That re-reads the product row too.

    // Exercise 4 — Summaries instead of graphs
    // Return one OrderSummary per order placed on `orderDate`, ordered by SalesOrderId: the order's
    // SalesOrderId, the customer's AccountNumber, its number of lines, and its TotalDue.
    // Rules: one round trip; nothing tracked; don't load the lines (count them in SQL).
    protected override Task<List<OrderSummary>> Ex4(E05Context db, DateTime orderDate)
        => db.SalesOrderHeaders
            .Where(h => h.OrderDate == orderDate)
            .OrderBy(h => h.SalesOrderId)
            .Select(h => new OrderSummary(h.SalesOrderId, h.Customer.AccountNumber, h.SalesOrderDetails.Count(), h.TotalDue))
            .ToListAsync();
    // Why: a projection needs no Include at all. Navigations used inside Select become joins and
    // subqueries, only the selected columns are read, and nothing is tracked or lazy-loaded. When you
    // only need a few values, projection beats loading entity graphs.

    // Exercise 5 — No N+1
    // Return every territory's Name and its number of customers, ordered by territory name.
    // Rules: one round trip, however many territories there are.
    protected override Task<List<TerritoryCustomers>> Ex5(E05Context db)
        => db.SalesTerritories
            .OrderBy(t => t.Name)
            .Select(t => new TerritoryCustomers(t.Name, t.Customers.Count()))
            .ToListAsync();
    // Why: Count() inside the projection is a correlated subquery, so the database counts. Tempting-but-
    // wrong: load the territories, then read t.Customers.Count in a loop. With lazy loading on, that is
    // one extra query per territory, each loading every customer of that territory just to count them.

    // Challenge 1 — No cartesian explosion
    // Return sales person `salesPersonId` with SalesOrderHeaders, Stores and SalesPersonQuotaHistories
    // all loaded. Loading them in one query would return orders × stores × quotas rows
    // (473 × 76 × 12 = 431,376 for sales person 277).
    // Rules: no query joins two collections to the parent; at most 4 round trips.
    protected override Task<SalesPerson> Ch1(E05Context db, int salesPersonId)
        => db.SalesPeople
            .Include(s => s.SalesOrderHeaders)
            .Include(s => s.Stores)
            .Include(s => s.SalesPersonQuotaHistories)
            .AsSplitQuery()
            .SingleAsync(s => s.BusinessEntityId == salesPersonId);
    // Why: AsSplitQuery sends one query for the sales person and one per collection (4 in total), so the
    // rows add up (1 + 473 + 76 + 12) instead of multiplying. The trade-off: the queries run at
    // different moments, so without a snapshot or serializable transaction a concurrent writer can make
    // them disagree. Three explicit loads would also pass, at the same 4 round trips.

    // Challenge 2 — The three most recent orders each
    // Return the customers with at least `minOrders` orders, ordered by CustomerId, each with only its
    // 3 most recent orders loaded (OrderDate descending, then SalesOrderId descending), in that order.
    // Rules: one round trip.
    protected override Task<List<Customer>> Ch2(E05Context db, int minOrders)
        => db.Customers
            .Where(c => c.SalesOrderHeaders.Count() >= minOrders)
            .OrderBy(c => c.CustomerId)
            .Include(c => c.SalesOrderHeaders
                .OrderByDescending(h => h.OrderDate)
                .ThenByDescending(h => h.SalesOrderId)
                .Take(3))
            .ToListAsync();
    // Why: a filtered Include accepts OrderBy, Skip and Take as well as Where. EF numbers each customer's
    // orders with ROW_NUMBER() OVER (PARTITION BY CustomerID ...) and keeps rows 1-3: the top-N-per-group
    // pattern from E03, applied to loading.

    // Challenge 3 — Load part, count the rest
    // `salesPerson` is already tracked by `db`. Load only its orders placed in calendar year `year` into
    // SalesOrderHeaders, and return its number of stores without loading them.
    // Rules: exactly two round trips; the Stores collection must stay unloaded.
    protected override async Task<int> Ch3(E05Context db, SalesPerson salesPerson, int year)
    {
        var entry = db.Entry(salesPerson);
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);
        await entry.Collection(s => s.SalesOrderHeaders).Query()
            .Where(h => h.OrderDate >= start && h.OrderDate < end)
            .LoadAsync();
        return await entry.Collection(s => s.Stores).Query().CountAsync();
    }
    // Why: Collection(...).Query() is the navigation as an IQueryable: filter it and Load to fill the
    // collection with just those rows, or aggregate it to answer a question without loading anything.
    // Note the trap the check avoids: a partly loaded collection is still "not loaded", so with lazy
    // loading on, the next read of salesPerson.SalesOrderHeaders would fetch all 473 orders.
}
