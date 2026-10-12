using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E11;

// E11 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E11Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E11Exercises : E11Checks
{
    // Exercise 1 — Table per hierarchy
    // Map Payment, CardPayment, BankTransfer and Voucher (E11Model.cs) to ONE table named Payments,
    // with the type stored in a discriminator column named Kind (nvarchar(10)) holding "card", "bank"
    // or "voucher".
    protected override void Ex1(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 2 — Table per type
    // Map the same hierarchy with TPT: a Payments table for the shared columns (Id, Amount), and one
    // table per subtype for its own columns: CardPayments, BankTransfers, Vouchers.
    protected override void Ex2(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 3 — Table per concrete type
    // Map the hierarchy with TPC: one complete table per concrete type (CardPayments, BankTransfers,
    // Vouchers, each with Id and Amount), no table for the abstract Payment, and Ids unique across all
    // three tables.
    protected override void Ex3(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 4 — JSON columns
    // Map Order so that Shipping (one object) and Notes (a list) are each stored as JSON in a single
    // column, named Shipping and Notes.
    protected override void Ex4(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 5 — Filter on a JSON value
    // Return the Ids of the orders shipped to `city`, ordered by Id. `db` uses Checks.OrderModel
    // (Shipping and Notes as JSON).
    // Rules: one round trip; the filter runs in SQL.
    protected override Task<List<int>> Ex5(LabModelContext db, string city)
        => throw new NotImplementedException();

    // Challenge 1 — Search a JSON collection
    // Return the Ids of the orders with at least one note whose Text contains `word`, ordered by Id.
    // Rules: one round trip; the search runs in SQL.
    protected override Task<List<int>> Ch1(LabModelContext db, string word)
        => throw new NotImplementedException();

    // Challenge 2 — Update inside JSON
    // Set Shipping.Carrier to `carrier` for every order shipped to `city`, in one statement, and return
    // the number of orders updated.
    // Rules: one round trip; load nothing.
    protected override Task<int> Ch2(LabModelContext db, string city, string carrier)
        => throw new NotImplementedException();

    // Challenge 3 — Totals per kind
    // `db` maps the payments with TPH (Checks.PaymentsInOneTable: discriminator column Kind). Return the
    // total Amount per Kind, ordered by Kind.
    // Rules: one round trip; group on the discriminator column itself.
    protected override Task<List<KindTotal>> Ch3(LabModelContext db)
        => throw new NotImplementedException();
}
