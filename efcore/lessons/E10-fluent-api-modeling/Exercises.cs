using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E10;

// E10 exercises. Replace each `throw new NotImplementedException()` with your answer, then run:
//   dotnet test --filter E10Exercises
// Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
// The checks are in Checks.cs. Peek at Solutions.cs only after attempting.
public class E10Exercises : E10Checks
{
    // Exercise 1 — Composite key
    // Map OrderLine (E10Model.cs) with the primary key (OrderId, LineNumber), in that order. Sku is
    // required and at most 20 characters.
    protected override void Ex1(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 2 — An optional relationship that sets NULL
    // Map Author and Book: a book may have no author, and deleting an author in the database sets its
    // books' AuthorId to NULL (ON DELETE SET NULL). Book.Title is at most 200 characters.
    protected override void Ex2(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 3 — A complex type
    // Map Customer.ShippingAddress as a complex type: its values live in the Customer table (no Address
    // table, no key), in columns named ShippingStreet and ShippingCity.
    protected override void Ex3(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 4 — An enum stored as text
    // Store Shipment.Status as its name ("Shipped") in an nvarchar(20) column.
    protected override void Ex4(ModelBuilder model)
        => throw new NotImplementedException();

    // Exercise 5 — A shadow property
    // Give Note a CreatedAt column (datetime2, NOT NULL) that is not a property of the Note class, filled
    // by the database with SYSUTCDATETIME() when a row is inserted.
    protected override void Ex5(ModelBuilder model)
        => throw new NotImplementedException();

    // Challenge 1 — Named query filters
    // Give Ticket two query filters that can be switched off independently: "SoftDelete" hides
    // IsDeleted tickets, "Archived" hides IsArchived tickets.
    protected override void Ch1(ModelBuilder model)
        => throw new NotImplementedException();

    // Challenge 2 — A backing field
    // Map Account.Balance, a read-only property backed by the private field ledgerBalance, as a
    // decimal(12, 2) column. Loading an account must set the field.
    protected override void Ch2(ModelBuilder model)
        => throw new NotImplementedException();

    // Challenge 3 — A relationship on an alternate key
    // Map Member and Visit so that Visit.MemberNumber refers to Member.MemberNumber (not to Member.Id),
    // and deleting a member deletes its visits.
    protected override void Ch3(ModelBuilder model)
        => throw new NotImplementedException();
}
