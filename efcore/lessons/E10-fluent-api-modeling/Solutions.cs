using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E10;

// Worked solutions for E10, each followed by a Why note. Run: dotnet test --filter E10Solutions
public class E10Solutions : E10Checks
{
    // Exercise 1 — Composite key
    // Map OrderLine (E10Model.cs) with the primary key (OrderId, LineNumber), in that order. Sku is
    // required and at most 20 characters.
    protected override void Ex1(ModelBuilder model)
        => model.Entity<OrderLine>(line =>
        {
            line.HasKey(l => new { l.OrderId, l.LineNumber });
            line.Property(l => l.Sku).HasMaxLength(20);
        });
    // Why: EF's key convention only recognizes a single property named Id or <Type>Id, and OrderLine
    // has neither, so without HasKey the model fails to build. A composite key is an anonymous type, in
    // column order. Sku is already required because it's a non-nullable string (nullable reference
    // types on); HasMaxLength turns nvarchar(max) into nvarchar(20).

    // Exercise 2 — An optional relationship that sets NULL
    // Map Author and Book: a book may have no author, and deleting an author in the database sets its
    // books' AuthorId to NULL (ON DELETE SET NULL). Book.Title is at most 200 characters.
    protected override void Ex2(ModelBuilder model)
    {
        model.Entity<Book>(book =>
        {
            book.Property(b => b.Title).HasMaxLength(200);
            book.HasOne(b => b.Author)
                .WithMany(a => a.Books)
                .HasForeignKey(b => b.AuthorId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
    // Why: the relationship is optional because AuthorId is int?, but the convention for optional
    // relationships is ClientSetNull: EF nulls the FK of tracked books, and the database's constraint
    // is NO ACTION, so a DELETE in SQL fails while books still point at the author. SetNull puts
    // ON DELETE SET NULL in the constraint, so the database does it for every book, tracked or not.

    // Exercise 3 — A complex type
    // Map Customer.ShippingAddress as a complex type: its values live in the Customer table (no Address
    // table, no key), in columns named ShippingStreet and ShippingCity.
    protected override void Ex3(ModelBuilder model)
        => model.Entity<Customer>().ComplexProperty(c => c.ShippingAddress, address =>
        {
            address.Property(a => a.Street).HasColumnName("ShippingStreet");
            address.Property(a => a.City).HasColumnName("ShippingCity");
        });
    // Why: Address has no key, so EF can't map it as an entity. A complex type is a value: it has no
    // identity, lives in its owner's row, and is compared by value. Its columns default to
    // ShippingAddress_Street; HasColumnName renames them. (An owned type, OwnsOne, also lives in the
    // owner's table, but it is still an entity type with a hidden key; complex types are EF 8+'s answer
    // for plain values.)

    // Exercise 4 — An enum stored as text
    // Store Shipment.Status as its name ("Shipped") in an nvarchar(20) column.
    protected override void Ex4(ModelBuilder model)
        => model.Entity<Shipment>().Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
    // Why: by default an enum is stored as its integer value. HasConversion<string>() adds a value
    // converter: names going in, Enum.Parse coming out. Queries still translate: Status == Shipped
    // becomes [Status] = N'Shipped'. Text survives reordering the enum, at the price of more bytes.

    // Exercise 5 — A shadow property
    // Give Note a CreatedAt column (datetime2, NOT NULL) that is not a property of the Note class, filled
    // by the database with SYSUTCDATETIME() when a row is inserted.
    protected override void Ex5(ModelBuilder model)
        => model.Entity<Note>().Property<DateTime>("CreatedAt").HasDefaultValueSql("SYSUTCDATETIME()");
    // Why: Property<T>("Name") declares a shadow property: part of the model and the table, but not of
    // the class. EF tracks its value in the change tracker, and queries reach it with
    // EF.Property<DateTime>(n, "CreatedAt"). HasDefaultValueSql leaves the value to the database on
    // insert, so the class stays free of audit plumbing.

    // Challenge 1 — Named query filters
    // Give Ticket two query filters that can be switched off independently: "SoftDelete" hides
    // IsDeleted tickets, "Archived" hides IsArchived tickets.
    protected override void Ch1(ModelBuilder model)
        => model.Entity<Ticket>()
            .HasQueryFilter("SoftDelete", t => !t.IsDeleted)
            .HasQueryFilter("Archived", t => !t.IsArchived);
    // Why: before EF 10 an entity type had one query filter, so disabling it switched off every rule at
    // once. Named filters (new in EF 10) are independent: IgnoreQueryFilters(["SoftDelete"]) shows
    // deleted tickets while still hiding archived ones, and IgnoreQueryFilters() switches all off.

    // Challenge 2 — A backing field
    // Map Account.Balance, a read-only property backed by the private field ledgerBalance, as a
    // decimal(12, 2) column. Loading an account must set the field.
    protected override void Ch2(ModelBuilder model)
        => model.Entity<Account>().Property(a => a.Balance)
            .HasField("ledgerBalance")
            .HasPrecision(12, 2);
    // Why: EF maps a read-only property only when it finds a backing field, and its conventions only
    // look for names like _balance, balance or m_balance. HasField names the field explicitly, and EF
    // reads and writes it directly, so Deposit stays the only way to change a balance in code.

    // Challenge 3 — A relationship on an alternate key
    // Map Member and Visit so that Visit.MemberNumber refers to Member.MemberNumber (not to Member.Id),
    // and deleting a member deletes its visits.
    protected override void Ch3(ModelBuilder model)
        => model.Entity<Visit>()
            .HasOne(v => v.Member)
            .WithMany(m => m.Visits)
            .HasForeignKey(v => v.MemberNumber)
            .HasPrincipalKey(m => m.MemberNumber)
            .OnDelete(DeleteBehavior.Cascade);
    // Why: a foreign key can point at any unique column, not only the primary key. HasPrincipalKey makes
    // MemberNumber an alternate key (a UNIQUE constraint) for the FK to reference. Without it, EF can't
    // use the string MemberNumber as an FK to the int Id, and adds a shadow MemberId column instead.
}
