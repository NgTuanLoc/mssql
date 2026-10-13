using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E11;

// Worked solutions for E11, each followed by a Why note. Run: dotnet test --filter E11Solutions
public class E11Solutions : E11Checks
{
    // Exercise 1 — Table per hierarchy
    // Map Payment, CardPayment, BankTransfer and Voucher (E11Model.cs) to ONE table named Payments,
    // with the type stored in a discriminator column named Kind (nvarchar(10)) holding "card", "bank"
    // or "voucher".
    protected override void Ex1(ModelBuilder model)
        => model.Entity<Payment>(payment =>
        {
            payment.ToTable("Payments");
            payment.HasDiscriminator<string>("Kind")
                .HasValue<CardPayment>("card")
                .HasValue<BankTransfer>("bank")
                .HasValue<Voucher>("voucher");
            payment.Property<string>("Kind").HasMaxLength(10);
        });
    // Why: TPH is EF's default for a hierarchy. Every subtype's columns go into one table (nullable,
    // since a card row has no Iban), and a discriminator column says which type a row is. Querying one
    // subtype is a single-table query with WHERE [Kind] = N'card'. HasValue registers each subtype and
    // its value; the column is a shadow property you can still configure.

    // Exercise 2 — Table per type
    // Map the same hierarchy with TPT: a Payments table for the shared columns (Id, Amount), and one
    // table per subtype for its own columns: CardPayments, BankTransfers, Vouchers.
    protected override void Ex2(ModelBuilder model)
    {
        model.Entity<Payment>().UseTptMappingStrategy().ToTable("Payments");
        model.Entity<CardPayment>().ToTable("CardPayments");
        model.Entity<BankTransfer>().ToTable("BankTransfers");
        model.Entity<Voucher>().ToTable("Vouchers");
    }
    // Why: TPT normalizes: no nullable subtype columns, and each subtype table's Id is also an FK to
    // Payments. The cost is joins: reading payments of any type LEFT JOINs every subtype table, and
    // inserting a card writes two rows. It is usually the slowest strategy for queries.

    // Exercise 3 — Table per concrete type
    // Map the hierarchy with TPC: one complete table per concrete type (CardPayments, BankTransfers,
    // Vouchers, each with Id and Amount), no table for the abstract Payment, and Ids unique across all
    // three tables.
    protected override void Ex3(ModelBuilder model)
    {
        model.Entity<Payment>().UseTpcMappingStrategy();
        model.Entity<CardPayment>().ToTable("CardPayments");
        model.Entity<BankTransfer>().ToTable("BankTransfers");
        model.Entity<Voucher>().ToTable("Vouchers");
    }
    // Why: TPC gives each concrete type a complete table, so a query for one type touches one table
    // with no discriminator and no join. Querying the base type is a UNION ALL. Keys must stay unique
    // across tables, so EF replaces IDENTITY with a shared sequence (PaymentSequence).

    // Exercise 4 — JSON columns
    // Map Order so that Shipping (one object) and Notes (a list) are each stored as JSON in a single
    // column, named Shipping and Notes.
    protected override void Ex4(ModelBuilder model)
        => model.Entity<Order>(order =>
        {
            order.ComplexProperty(o => o.Shipping, shipping => shipping.ToJson());
            order.ComplexCollection(o => o.Notes, notes => notes.ToJson());
        });
    // Why: ToJson() serializes the value into one nvarchar(max) column (on SQL Server 2022; SQL Server
    // 2025 and Azure SQL can use the native json type). Complex types are the EF 10 way to map JSON;
    // owned types with ToJson() also work, but carry entity semantics a value doesn't need.

    // Exercise 5 — Filter on a JSON value
    // Return the Ids of the orders shipped to `city`, ordered by Id. `db` uses Checks.OrderModel
    // (Shipping and Notes as JSON).
    // Rules: one round trip; the filter runs in SQL.
    protected override Task<List<int>> Ex5(LabModelContext db, string city)
        => db.Set<Order>()
            .Where(o => o.Shipping.City == city)
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .ToListAsync();
    // Why: EF translates a path into JSON like a column: o.Shipping.City becomes
    // JSON_VALUE([o].[Shipping], '$.City'). No deserializing on the client. Note that SQL Server can't
    // seek an index on a value inside JSON unless you add a computed column for it.

    // Challenge 1 — Search a JSON collection
    // Return the Ids of the orders with at least one note whose Text contains `word`, ordered by Id.
    // Rules: one round trip; the search runs in SQL.
    protected override Task<List<int>> Ch1(LabModelContext db, string word)
        => db.Set<Order>()
            .Where(o => o.Notes.Any(n => n.Text.Contains(word)))
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .ToListAsync();
    // Why: a JSON array is queryable like a collection: EF expands it with OPENJSON(...) WITH (...) and
    // turns Any into EXISTS over those rows. Loading the orders to search their notes in C# would ship
    // every order and its JSON.

    // Challenge 2 — Update inside JSON
    // Set Shipping.Carrier to `carrier` for every order shipped to `city`, in one statement, and return
    // the number of orders updated.
    // Rules: one round trip; load nothing.
    protected override Task<int> Ch2(LabModelContext db, string city, string carrier)
        => db.Set<Order>()
            .Where(o => o.Shipping.City == city)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.Shipping.Carrier, carrier));
    // Why: EF 10 lets ExecuteUpdate set a property inside a JSON column. It becomes
    // SET [o].[Shipping] = JSON_MODIFY([o].[Shipping], '$.Carrier', @p), changing that one value in
    // place. Before EF 10, ExecuteUpdate couldn't reach into a JSON column, so you loaded each order,
    // changed it and called SaveChanges (EF 7+ already sends a partial JSON_MODIFY for that, but only
    // after loading every row).

    // Challenge 3 — Totals per kind
    // `db` maps the payments with TPH (Checks.PaymentsInOneTable: discriminator column Kind). Return the
    // total Amount per Kind, ordered by Kind.
    // Rules: one round trip; group on the discriminator column itself.
    protected override Task<List<KindTotal>> Ch3(LabModelContext db)
        => db.Set<Payment>()
            .GroupBy(p => EF.Property<string>(p, "Kind"))
            .OrderBy(g => g.Key)
            .Select(g => new KindTotal(g.Key, g.Sum(p => p.Amount)))
            .ToListAsync();
    // Why: the discriminator is a shadow property, so EF.Property<string>(p, "Kind") reaches it like any
    // column and the GROUP BY is on [Kind]. Tempting-but-wrong: grouping on p is CardPayment ? "card" :
    // ..., which works but makes SQL compute a CASE for every row. Order before projecting into the
    // record: EF can't compose after a positional record's constructor (E02).
}
