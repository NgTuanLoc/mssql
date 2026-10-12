using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E10;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E10Examples --logger "console;verbosity=detailed"
// Each example builds its own model in EfLab_E10, inside the test's transaction.
public class E10Examples() : CodeFirstTest("E10")
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    public class Wallet
    {
        decimal _balance; // a name EF's conventions recognize
        public int Id { get; set; }
        public decimal Balance => _balance;
        public void Add(decimal amount) => _balance += amount;
    }

    // Concept 1: with no configuration, conventions decide tables, keys, types and relationships.
    [Fact]
    public async Task Conventions_build_a_model_from_the_classes()
    {
        var db = await CreateModelAsync(model => model.Entity<Book>());
        Print(db.Database.GenerateCreateScript());

        Assert.Equal(["Author", "Book"], await Reference.TablesAsync()); // Author found through Book.Author
        Assert.Equal(["Id"], await Reference.PrimaryKeyAsync("Book"));   // "Id" is the key by convention
        Assert.Contains(new ColumnInfo("Title", "nvarchar", -1, false), await Reference.ColumnsAsync("Book")); // nvarchar(max) (-1), required
        Assert.Equal(["AuthorId -> Author.Id ON DELETE NO_ACTION"], await Reference.ForeignKeysAsync("Book"));   // optional: ClientSetNull
    }

    // Concept 2: a navigation without an FK property gets a shadow FK, required ones cascade.
    [Fact]
    public async Task A_missing_foreign_key_property_becomes_a_shadow_property()
    {
        var db = await CreateModelAsync(model => model.Entity<Visit>().Ignore(v => v.MemberNumber));

        Assert.Equal(["MemberId -> Member.Id ON DELETE CASCADE"], await Reference.ForeignKeysAsync("Visit"));
        Assert.True(db.Model.FindEntityType(typeof(Visit))!.FindProperty("MemberId")!.IsShadowProperty());
    }

    // Concept 2: DeleteBehavior decides both what EF does to tracked dependents and the ON DELETE rule.
    [Fact]
    public async Task Delete_behaviors_in_the_schema()
    {
        await CreateModelAsync(model =>
        {
            model.Entity<Book>().HasOne(b => b.Author).WithMany(a => a.Books).OnDelete(DeleteBehavior.Restrict);
            model.Entity<Visit>().Ignore(v => v.MemberNumber)
                .HasOne(v => v.Member).WithMany(m => m.Visits).OnDelete(DeleteBehavior.Cascade);
        });

        Assert.Equal(["AuthorId -> Author.Id ON DELETE NO_ACTION"], await Reference.ForeignKeysAsync("Book")); // Restrict: no action in SQL
        Assert.Equal(["MemberId -> Member.Id ON DELETE CASCADE"], await Reference.ForeignKeysAsync("Visit"));
    }

    // Concept 3: owned type vs complex type — same columns, different kinds of model object.
    [Fact]
    public void Owned_type_vs_complex_type()
    {
        var owned = NewContext(model => model.Entity<Customer>().OwnsOne(c => c.ShippingAddress));
        var complex = NewContext(model => model.Entity<Customer>().ComplexProperty(c => c.ShippingAddress));

        var address = owned.Model.FindEntityType(typeof(Customer))!.GetNavigations().Single().TargetEntityType;
        Assert.True(address.IsOwned());               // an entity type, with a hidden key
        Assert.Contains("ShippingAddress_City", owned.Database.GenerateCreateScript());
        Assert.NotNull(complex.Model.FindEntityType(typeof(Customer))!.FindComplexProperty("ShippingAddress")); // a value, no key
        Assert.Contains("ShippingAddress_City", complex.Database.GenerateCreateScript());
    }

    // Concept 4: a value converter changes what is stored; queries still translate.
    [Fact]
    public async Task Value_converter_enum_as_string()
    {
        var db = await CreateModelAsync(model =>
            model.Entity<Shipment>().Property(s => s.Status).HasConversion<string>().HasMaxLength(20));
        db.Add(new Shipment { Status = ShipmentStatus.Delivered });
        await db.SaveChangesAsync();

        Assert.Equal("Delivered", (await Reference.Rows<string>($"SELECT Status AS Value FROM [Shipment]")).Single());
        var sql = db.Set<Shipment>().Where(s => s.Status == ShipmentStatus.Delivered).ToQueryString();
        Assert.Contains("[s].[Status] = N'Delivered'", sql);
    }

    // Concept 5: shadow properties live in the model and the tracker, not the class.
    [Fact]
    public async Task Shadow_properties_through_Entry_and_EF_Property()
    {
        var db = await CreateModelAsync(model => model.Entity<Note>().Property<string>("Author").HasMaxLength(50));
        var note = new Note { Text = "hi" };
        db.Add(note);
        db.Entry(note).Property("Author").CurrentValue = "ana";
        await db.SaveChangesAsync();

        var authors = await NewContext(model => model.Entity<Note>().Property<string>("Author").HasMaxLength(50))
            .Set<Note>().Where(n => EF.Property<string>(n, "Author") == "ana").CountAsync();
        Assert.Equal(1, authors);
    }

    // Concept 5: a backing field with a conventional name is found automatically.
    [Fact]
    public async Task Conventional_backing_field_is_found()
    {
        var db = await CreateModelAsync(model => model.Entity<Wallet>().Property(w => w.Balance).HasPrecision(12, 2));
        var wallet = new Wallet();
        wallet.Add(42m);
        db.Add(wallet);
        await db.SaveChangesAsync();

        Assert.Equal("_balance", db.Model.FindEntityType(typeof(Wallet))!.FindProperty("Balance")!.FieldInfo!.Name);
        Assert.Equal(42m, (await Reference.Rows<decimal>($"SELECT Balance AS Value FROM [Wallet]")).Single());
    }

    // Concept 6: named query filters add to every query and can be ignored one by one.
    [Fact]
    public void Named_query_filters_in_SQL()
    {
        var db = NewContext(model => model.Entity<Ticket>()
            .HasQueryFilter("SoftDelete", t => !t.IsDeleted)
            .HasQueryFilter("Archived", t => !t.IsArchived));

        var both = db.Set<Ticket>().ToQueryString();
        var onlyArchived = db.Set<Ticket>().IgnoreQueryFilters(["SoftDelete"]).ToQueryString();

        Assert.Contains("[t].[IsDeleted] = CAST(0 AS bit) AND [t].[IsArchived] = CAST(0 AS bit)", both);
        Assert.DoesNotContain("IsDeleted] =", onlyArchived);
        Assert.Contains("[t].[IsArchived] = CAST(0 AS bit)", onlyArchived);
    }
}
