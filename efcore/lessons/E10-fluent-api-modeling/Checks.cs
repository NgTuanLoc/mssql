using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E10;

// The checks for E10. E10Exercises (yours) and E10Solutions both inherit them.
// Each check builds a model from your configuration method, creates its tables in the EfLab_E10
// database inside the test's transaction, then checks the schema EF created and how it behaves.
public abstract class E10Checks() : CodeFirstTest("E10")
{
    protected abstract void Ex1(ModelBuilder model);
    protected abstract void Ex2(ModelBuilder model);
    protected abstract void Ex3(ModelBuilder model);
    protected abstract void Ex4(ModelBuilder model);
    protected abstract void Ex5(ModelBuilder model);
    protected abstract void Ch1(ModelBuilder model);
    protected abstract void Ch2(ModelBuilder model);
    protected abstract void Ch3(ModelBuilder model);

    [Exercise]
    public async Task Ex1_CompositeKey()
    {
        var db = await CreateModelAsync(Ex1);

        Assert.Equal(["OrderId", "LineNumber"], await Reference.PrimaryKeyAsync("OrderLine"));
        Assert.Contains(new ColumnInfo("Sku", "nvarchar", 20, false), await Reference.ColumnsAsync("OrderLine"));

        db.AddRange(
            new OrderLine { OrderId = 1, LineNumber = 1, Sku = "A", Quantity = 1 },
            new OrderLine { OrderId = 2, LineNumber = 1, Sku = "B", Quantity = 1 }); // same line number, another order
        await db.SaveChangesAsync();
        var duplicate = NewContext(Ex1);
        duplicate.Add(new OrderLine { OrderId = 1, LineNumber = 1, Sku = "C", Quantity = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync()); // the database enforces the key
    }

    [Exercise]
    public async Task Ex2_OptionalRelationshipThatSetsNull()
    {
        var db = await CreateModelAsync(Ex2);

        Assert.Equal(["AuthorId -> Author.Id ON DELETE SET_NULL"], await Reference.ForeignKeysAsync("Book"));
        Assert.Contains(new ColumnInfo("Title", "nvarchar", 200, false), await Reference.ColumnsAsync("Book"));

        db.Add(new Author { Name = "A", Books = [new Book { Title = "One" }, new Book { Title = "Two" }] });
        db.Add(new Book { Title = "Anonymous" }); // a book with no author is allowed
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM [Author]"); // the database, not EF, handles the books
        Assert.Equal(3, (await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM [Book] WHERE AuthorId IS NULL")).Single());
    }

    [Exercise]
    public async Task Ex3_ComplexType()
    {
        var db = await CreateModelAsync(Ex3);

        Assert.Equal(["Customer"], await Reference.TablesAsync()); // no Address table
        var columns = (await Reference.ColumnsAsync("Customer")).Select(c => c.Column).ToList();
        Assert.Equal(["Id", "Name", "ShippingCity", "ShippingStreet"], columns.Order());
        Assert.NotNull(db.Model.FindEntityType(typeof(Customer))!.FindComplexProperty(nameof(Customer.ShippingAddress)));

        db.Add(new Customer { Name = "C", ShippingAddress = new Address { Street = "1 Main St", City = "Seattle" } });
        await db.SaveChangesAsync();
        var city = await NewContext(Ex3).Set<Customer>().Select(c => c.ShippingAddress.City).SingleAsync();
        Assert.Equal("Seattle", city);
    }

    [Exercise]
    public async Task Ex4_EnumAsText()
    {
        var db = await CreateModelAsync(Ex4);

        Assert.Contains(new ColumnInfo("Status", "nvarchar", 20, false), await Reference.ColumnsAsync("Shipment"));

        db.AddRange(new Shipment { Status = ShipmentStatus.Shipped }, new Shipment { Status = ShipmentStatus.Pending });
        await db.SaveChangesAsync();
        Assert.Equal(["Pending", "Shipped"], (await Reference.Rows<string>($"SELECT Status AS Value FROM [Shipment]")).Order());
        Sql.Reset();
        Assert.Equal(1, await db.Set<Shipment>().CountAsync(s => s.Status == ShipmentStatus.Shipped));
        Assert.Contains("N'Shipped'", Sql.All); // the filter compares text in SQL
    }

    [Exercise]
    public async Task Ex5_ShadowProperty()
    {
        var db = await CreateModelAsync(Ex5);

        Assert.Contains(new ColumnInfo("CreatedAt", "datetime2", null, false), await Reference.ColumnsAsync("Note"));
        Assert.True(db.Model.FindEntityType(typeof(Note))!.FindProperty("CreatedAt")!.IsShadowProperty());
        var defaultSql = (await Reference.Rows<string>(
            $"SELECT LOWER(definition) AS Value FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('Note')")).Single();
        Assert.Contains("sysutcdatetime()", defaultSql); // computed by the database at insert time, not a constant

        var before = DateTime.UtcNow.AddMinutes(-5); // generous: the database's clock, not this machine's
        db.Add(new Note { Text = "hello" });
        await db.SaveChangesAsync();
        var createdAt = await NewContext(Ex5).Set<Note>().Select(n => EF.Property<DateTime>(n, "CreatedAt")).SingleAsync();
        Assert.InRange(createdAt, before, DateTime.UtcNow.AddMinutes(5));
    }

    [Exercise]
    public async Task Ch1_NamedQueryFilters()
    {
        var db = await CreateModelAsync(Ch1);
        db.AddRange(
            new Ticket { Title = "open" },
            new Ticket { Title = "deleted", IsDeleted = true },
            new Ticket { Title = "archived", IsArchived = true },
            new Ticket { Title = "both", IsDeleted = true, IsArchived = true });
        await db.SaveChangesAsync();
        var tickets = NewContext(Ch1).Set<Ticket>();

        Assert.Equal(["open"], await tickets.Select(t => t.Title).ToListAsync());
        Assert.Equal(["deleted", "open"], (await tickets.IgnoreQueryFilters(["SoftDelete"]).Select(t => t.Title).ToListAsync()).Order());
        Assert.Equal(["archived", "open"], (await tickets.IgnoreQueryFilters(["Archived"]).Select(t => t.Title).ToListAsync()).Order());
        Assert.Equal(4, await tickets.IgnoreQueryFilters().CountAsync());
    }

    [Exercise]
    public async Task Ch2_BackingField()
    {
        var db = await CreateModelAsync(Ch2);

        var balance = Assert.Single(await Reference.ColumnsAsync("Account"), c => c.Column == "Balance");
        Assert.Equal("decimal", balance.Type);
        Assert.Equal(12, db.Model.FindEntityType(typeof(Account))!.FindProperty(nameof(Account.Balance))!.GetPrecision());
        Assert.Equal(2, db.Model.FindEntityType(typeof(Account))!.FindProperty(nameof(Account.Balance))!.GetScale());

        var account = new Account();
        account.Deposit(100m);
        account.Deposit(50m);
        db.Add(account);
        await db.SaveChangesAsync();
        Assert.Equal(150m, (await NewContext(Ch2).Set<Account>().SingleAsync()).Balance); // materialized through the field
    }

    [Exercise]
    public async Task Ch3_RelationshipOnAnAlternateKey()
    {
        var db = await CreateModelAsync(Ch3);

        Assert.Equal(["MemberNumber -> Member.MemberNumber ON DELETE CASCADE"], await Reference.ForeignKeysAsync("Visit"));
        Assert.DoesNotContain(await Reference.ColumnsAsync("Visit"), c => c.Column == "MemberId"); // no shadow FK

        db.Add(new Member { MemberNumber = "M-001", Visits = [new Visit { At = new DateTime(2026, 10, 1) }] });
        await db.SaveChangesAsync();
        Assert.Equal("M-001", (await Reference.Rows<string>($"SELECT MemberNumber AS Value FROM [Visit]")).Single());
        var visit = await NewContext(Ch3).Set<Visit>().Include(v => v.Member).SingleAsync();
        Assert.Equal("M-001", visit.Member.MemberNumber);
    }
}
