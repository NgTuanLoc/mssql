namespace EfCoreLessons.E11;

// The classes the E11 exercises map. No attributes: every mapping decision is in the Fluent API.

// Exercises 1-3 and challenge 3: one hierarchy, three ways to store it.
public abstract class Payment
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

public class CardPayment : Payment
{
    public string Last4 { get; set; } = "";
}

public class BankTransfer : Payment
{
    public string Iban { get; set; } = "";
}

public class Voucher : Payment
{
    public string Code { get; set; } = "";
}

// Exercise 4, exercise 5 and challenges 1-2: an order whose shipping details and notes live in JSON.
public class Order
{
    public int Id { get; set; }
    public string Customer { get; set; } = "";
    public Shipping Shipping { get; set; } = new();
    public List<OrderNote> Notes { get; set; } = [];
}

public class Shipping
{
    public string City { get; set; } = "";
    public string Carrier { get; set; } = "";
}

public class OrderNote
{
    public string Text { get; set; } = "";
    public DateTime At { get; set; }
}
