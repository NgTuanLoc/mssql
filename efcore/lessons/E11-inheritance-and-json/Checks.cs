using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E11;

public record KindTotal(string Kind, decimal Total);

// The checks for E11. E11Exercises (yours) and E11Solutions both inherit them.
// Exercises 1-4 are model configurations, checked against the schema they create in EfLab_E11.
// Exercise 5 and the challenges are queries against the models below.
public abstract class E11Checks() : CodeFirstTest("E11")
{
    protected abstract void Ex1(ModelBuilder model);
    protected abstract void Ex2(ModelBuilder model);
    protected abstract void Ex3(ModelBuilder model);
    protected abstract void Ex4(ModelBuilder model);
    protected abstract Task<List<int>> Ex5(LabModelContext db, string city);
    protected abstract Task<List<int>> Ch1(LabModelContext db, string word);
    protected abstract Task<int> Ch2(LabModelContext db, string city, string carrier);
    protected abstract Task<List<KindTotal>> Ch3(LabModelContext db);

    // The model exercise 5 and challenges 1-2 query: Order with its Shipping and Notes in JSON columns.
    protected static void OrderModel(ModelBuilder model) => model.Entity<Order>(order =>
    {
        order.ComplexProperty(o => o.Shipping, shipping => shipping.ToJson());
        order.ComplexCollection(o => o.Notes, notes => notes.ToJson());
    });

    // The model challenge 3 queries: the payments in one table (TPH), the type in a "Kind" column.
    protected static void PaymentsInOneTable(ModelBuilder model) => model.Entity<Payment>()
        .HasDiscriminator<string>("Kind")
        .HasValue<CardPayment>("card")
        .HasValue<BankTransfer>("bank")
        .HasValue<Voucher>("voucher");

    static Payment[] OneOfEach() =>
        [new CardPayment { Amount = 10m, Last4 = "4242" }, new BankTransfer { Amount = 100m, Iban = "DE89" }, new Voucher { Amount = 7m, Code = "XMAS" }];

    // Three orders: 1 Seattle/DHL with an urgent note, 2 Paris/DHL without notes, 3 Seattle/UPS.
    async Task<LabModelContext> OrdersAsync()
    {
        var db = await CreateModelAsync(OrderModel);
        db.AddRange(
            new Order { Customer = "a", Shipping = new() { City = "Seattle", Carrier = "DHL" }, Notes = [new() { Text = "urgent: call first", At = new DateTime(2026, 10, 1) }] },
            new Order { Customer = "b", Shipping = new() { City = "Paris", Carrier = "DHL" } },
            new Order { Customer = "c", Shipping = new() { City = "Seattle", Carrier = "UPS" }, Notes = [new() { Text = "leave at the door", At = new DateTime(2026, 10, 2) }] });
        await db.SaveChangesAsync();
        Sql.Reset();
        return NewContext(OrderModel); // nothing tracked
    }

    async Task<List<string>> ColumnNames(string table) => (await Reference.ColumnsAsync(table)).Select(c => c.Column).ToList();

    [Exercise]
    public async Task Ex1_TablePerHierarchy()
    {
        var db = await CreateModelAsync(Ex1);

        Assert.Equal(["Payments"], await Reference.TablesAsync());
        Assert.Contains(new ColumnInfo("Kind", "nvarchar", 10, false), await Reference.ColumnsAsync("Payments"));
        db.AddRange(OneOfEach());
        await db.SaveChangesAsync();
        Assert.Equal(["bank", "card", "voucher"], (await Reference.Rows<string>($"SELECT Kind AS Value FROM Payments")).Order());
        Sql.Reset();
        Assert.Equal(1, await db.Set<CardPayment>().CountAsync());
        Assert.Contains("[p].[Kind] = N'card'", Sql.All); // one table, filtered by the discriminator
    }

    [Exercise]
    public async Task Ex2_TablePerType()
    {
        var db = await CreateModelAsync(Ex2);

        Assert.Equal(["BankTransfers", "CardPayments", "Payments", "Vouchers"], await Reference.TablesAsync());
        Assert.Equal(["Id", "Last4"], await ColumnNames("CardPayments")); // Amount lives in Payments only
        db.AddRange(OneOfEach());
        await db.SaveChangesAsync();
        Sql.Reset();
        Assert.Equal(3, (await db.Set<Payment>().ToListAsync()).Count);
        Assert.Contains("LEFT JOIN [CardPayments]", Sql.All); // the base query joins every subtype table
    }

    [Exercise]
    public async Task Ex3_TablePerConcreteType()
    {
        var db = await CreateModelAsync(Ex3);

        Assert.Equal(["BankTransfers", "CardPayments", "Vouchers"], await Reference.TablesAsync()); // no table for the abstract base
        Assert.Equal(["Id", "Amount", "Last4"], await ColumnNames("CardPayments"));                // each table is complete
        var payments = OneOfEach();
        db.AddRange(payments);
        await db.SaveChangesAsync();
        Assert.Equal(3, payments.Select(p => p.Id).Distinct().Count()); // keys unique across the three tables
        Sql.Reset();
        Assert.Equal(3, (await db.Set<Payment>().ToListAsync()).Count);
        Assert.Contains("UNION ALL", Sql.All);
    }

    [Exercise]
    public async Task Ex4_JsonColumns()
    {
        var db = await CreateModelAsync(Ex4);

        Assert.Equal(["Order"], await Reference.TablesAsync());
        Assert.Equal(["Customer", "Id", "Notes", "Shipping"], (await ColumnNames("Order")).Order());
        db.Add(new Order { Customer = "a", Shipping = new() { City = "Seattle", Carrier = "DHL" }, Notes = [new() { Text = "hi", At = new DateTime(2026, 10, 1) }] });
        await db.SaveChangesAsync();
        var json = (await Reference.Rows<string>($"SELECT Shipping AS Value FROM [Order]")).Single();
        Assert.Contains("\"City\":\"Seattle\"", json);
        var loaded = await NewContext(Ex4).Set<Order>().SingleAsync();
        Assert.Equal("Seattle", loaded.Shipping.City);
        Assert.Equal("hi", Assert.Single(loaded.Notes).Text);
    }

    [Exercise]
    public async Task Ex5_FilterOnAJsonValue()
    {
        var db = await OrdersAsync();

        Assert.Equal([1, 3], await Ex5(db, "Seattle"));
        Sql.AssertRoundTrips(1);
        Assert.Contains("JSON_VALUE([o].[Shipping], '$.City')", Sql.All); // filtered inside the JSON, in SQL
    }

    [Exercise]
    public async Task Ch1_SearchAJsonCollection()
    {
        var db = await OrdersAsync();

        Assert.Equal([1], await Ch1(db, "urgent"));
        Sql.AssertRoundTrips(1);
        Assert.Contains("OPENJSON([o].[Notes]", Sql.All); // the notes array, expanded in SQL
    }

    [Exercise]
    public async Task Ch2_UpdateInsideJson()
    {
        var db = await OrdersAsync();

        Assert.Equal(2, await Ch2(db, "Seattle", "FedEx"));

        Sql.AssertRoundTrips(1);
        Assert.Contains("JSON_MODIFY(", Sql.All);
        var carriers = await Reference.Rows<string>($"SELECT JSON_VALUE(Shipping, '$.Carrier') AS Value FROM [Order] ORDER BY Id");
        Assert.Equal(["FedEx", "DHL", "FedEx"], carriers);
    }

    [Exercise]
    public async Task Ch3_TotalsPerKind()
    {
        var db = await CreateModelAsync(PaymentsInOneTable);
        db.AddRange([.. OneOfEach(), new CardPayment { Amount = 5m, Last4 = "1111" }]);
        await db.SaveChangesAsync();
        Sql.Reset();

        var totals = await Ch3(NewContext(PaymentsInOneTable));

        Assert.Equal([new KindTotal("bank", 100m), new KindTotal("card", 15m), new KindTotal("voucher", 7m)], totals);
        Sql.AssertRoundTrips(1);
        Assert.Contains("GROUP BY [p].[Kind]", Sql.All); // grouped on the discriminator column itself
    }
}
