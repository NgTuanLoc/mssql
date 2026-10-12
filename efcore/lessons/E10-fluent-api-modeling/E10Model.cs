namespace EfCoreLessons.E10;

// The classes the E10 exercises configure. They carry no attributes: every mapping decision is made
// in the Fluent API. Each exercise maps its own small set of classes, so exercises don't interfere.

// Exercise 1 (keys)
public class OrderLine
{
    public int OrderId { get; set; }
    public int LineNumber { get; set; }
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
}

// Exercise 2 (relationships and delete behavior)
public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Book> Books { get; set; } = [];
}

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int? AuthorId { get; set; }
    public Author? Author { get; set; }
}

// Exercise 3 (complex types)
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Address ShippingAddress { get; set; } = new();
}

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
}

// Exercise 4 (value converters)
public enum ShipmentStatus { Pending, Shipped, Delivered }

public class Shipment
{
    public int Id { get; set; }
    public ShipmentStatus Status { get; set; }
}

// Exercise 5 (shadow properties)
public class Note
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
}

// Challenge 1 (named query filters)
public class Ticket
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsDeleted { get; set; }
    public bool IsArchived { get; set; }
}

// Challenge 2 (backing fields)
public class Account
{
    decimal ledgerBalance; // not a name EF's conventions recognize as Balance's backing field

    public int Id { get; set; }
    public decimal Balance => ledgerBalance;

    public void Deposit(decimal amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        ledgerBalance += amount;
    }
}

// Challenge 3 (alternate keys)
public class Member
{
    public int Id { get; set; }
    public string MemberNumber { get; set; } = "";
    public List<Visit> Visits { get; set; } = [];
}

public class Visit
{
    public int Id { get; set; }
    public string MemberNumber { get; set; } = "";
    public DateTime At { get; set; }
    public Member Member { get; set; } = null!;
}
