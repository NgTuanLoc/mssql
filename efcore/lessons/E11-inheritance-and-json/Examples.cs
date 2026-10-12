using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E11;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E11Examples --logger "console;verbosity=detailed"
// Each example builds its own model in EfLab_E11, inside the test's transaction.
public class E11Examples() : CodeFirstTest("E11")
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    static void Tph(ModelBuilder model) { model.Entity<Payment>(); model.Entity<CardPayment>(); model.Entity<BankTransfer>(); model.Entity<Voucher>(); }
    static void Tpt(ModelBuilder model) { model.Entity<Payment>().UseTptMappingStrategy(); model.Entity<CardPayment>(); model.Entity<BankTransfer>(); model.Entity<Voucher>(); }
    static void Tpc(ModelBuilder model) { model.Entity<Payment>().UseTpcMappingStrategy(); model.Entity<CardPayment>(); model.Entity<BankTransfer>(); model.Entity<Voucher>(); }

    // Concept 1: with no strategy configured, a hierarchy is one table plus a Discriminator column.
    [Fact]
    public async Task Default_mapping_is_TPH()
    {
        var db = await CreateModelAsync(Tph);
        Print(db.Database.GenerateCreateScript());

        Assert.Equal(["Payment"], await Reference.TablesAsync());
        var columns = await Reference.ColumnsAsync("Payment");
        Assert.Contains(new ColumnInfo("Discriminator", "nvarchar", 13, false), columns); // EF sizes it from the type names
        Assert.Contains(new ColumnInfo("Iban", "nvarchar", -1, true), columns);           // subtype columns are nullable
    }

    // Concept 2: the same query for one subtype, in each strategy.
    [Fact]
    public void One_subtype_query_in_each_strategy()
    {
        var tph = NewContext(Tph).Set<CardPayment>().ToQueryString();
        var tpt = NewContext(Tpt).Set<CardPayment>().ToQueryString();
        var tpc = NewContext(Tpc).Set<CardPayment>().ToQueryString();
        Print($"TPH:\n{tph}\n\nTPT:\n{tpt}\n\nTPC:\n{tpc}");

        Assert.Contains("WHERE [p].[Discriminator] = N'CardPayment'", tph); // one table, filtered
        Assert.Contains("INNER JOIN [CardPayment]", tpt);                    // base table + subtype table
        Assert.DoesNotContain("JOIN", tpc);                                  // one complete table
        Assert.DoesNotContain("WHERE", tpc);
    }

    // Concept 2: the same query for the base type, in each strategy.
    [Fact]
    public void Base_type_query_in_each_strategy()
    {
        var tph = NewContext(Tph).Set<Payment>().ToQueryString();
        var tpt = NewContext(Tpt).Set<Payment>().ToQueryString();
        var tpc = NewContext(Tpc).Set<Payment>().ToQueryString();
        Print($"TPH:\n{tph}\n\nTPT:\n{tpt}\n\nTPC:\n{tpc}");

        Assert.DoesNotContain("JOIN", tph);
        Assert.Equal(3, tpt.Split("LEFT JOIN").Length - 1); // one per subtype
        Assert.Equal(2, tpc.Split("UNION ALL").Length - 1); // three tables stacked
    }

    // Concept 2: TPC keys come from a shared sequence so they stay unique across tables.
    [Fact]
    public async Task TPC_keys_come_from_a_sequence()
    {
        var db = await CreateModelAsync(Tpc);
        Assert.Contains("CREATE SEQUENCE [PaymentSequence]", db.Database.GenerateCreateScript());

        var card = new CardPayment { Amount = 1m, Last4 = "4242" };
        var voucher = new Voucher { Amount = 2m, Code = "X" };
        db.AddRange(card, voucher);
        await db.SaveChangesAsync();
        Assert.NotEqual(card.Id, voucher.Id);
    }

    // Concept 3: owned and complex types both map to one JSON column with ToJson().
    [Fact]
    public void Owned_or_complex_both_store_JSON()
    {
        var owned = NewContext(model => model.Entity<Order>(o =>
        {
            o.OwnsOne(x => x.Shipping, s => s.ToJson());
            o.OwnsMany(x => x.Notes, n => n.ToJson());
        }));
        var complex = NewContext(model => model.Entity<Order>(o =>
        {
            o.ComplexProperty(x => x.Shipping, s => s.ToJson());
            o.ComplexCollection(x => x.Notes, n => n.ToJson());
        }));

        foreach (var script in new[] { owned.Database.GenerateCreateScript(), complex.Database.GenerateCreateScript() })
        {
            Assert.Contains("[Shipping] nvarchar(max)", script); // one column for the whole object
            Assert.Contains("[Notes] nvarchar(max)", script);    // one column for the whole list
        }
    }

    // Concept 4: paths into JSON translate to JSON_VALUE and OPENJSON.
    [Fact]
    public async Task Querying_into_JSON()
    {
        var db = await CreateModelAsync(model => model.Entity<Order>(o =>
        {
            o.ComplexProperty(x => x.Shipping, s => s.ToJson());
            o.ComplexCollection(x => x.Notes, n => n.ToJson());
        }));
        db.Add(new Order
        {
            Customer = "a",
            Shipping = new() { City = "Oslo", Carrier = "Posten" },
            Notes = [new() { Text = "one", At = new DateTime(2026, 1, 1) }, new() { Text = "two", At = new DateTime(2026, 1, 2) }],
        });
        await db.SaveChangesAsync();
        Sql.Reset();

        var row = await db.Set<Order>()
            .Select(o => new { o.Shipping.City, NoteCount = o.Notes.Count })
            .SingleAsync();

        Assert.Equal("Oslo", row.City);
        Assert.Equal(2, row.NoteCount);
        Assert.Contains("JSON_VALUE([o].[Shipping], '$.City')", Sql.All);
        Assert.Contains("OPENJSON([o].[Notes]", Sql.All);
    }
}
