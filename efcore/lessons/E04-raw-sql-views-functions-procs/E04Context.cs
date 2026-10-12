using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E04;

// One row returned by dbo.ufnGetContactInformation. Keyless: a function returns rows, not entities.
public class ContactInformation
{
    public int PersonId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? JobTitle { get; set; }
    public string? BusinessEntityType { get; set; }
}

// E04's model: the shared AdventureWorks model plus two function mappings. A derived context gets
// its own cached model, so these mappings never leak into other lessons. Model/ stays untouched.
public class E04Context(DbContextOptions<AdventureWorksContext> options) : AdventureWorksContext(options)
{
    // dbo.ufnGetContactInformation(@PersonID) is table-valued: it returns IQueryable and composes with LINQ.
    public IQueryable<ContactInformation> GetContactInformation(int personId)
        => FromExpression(() => GetContactInformation(personId));

    // dbo.ufnGetStock(@ProductID) is scalar. The body never runs: EF translates calls inside a query.
    public static int GetStock(int productId)
        => throw new NotSupportedException("GetStock can only be used inside a LINQ query.");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ContactInformation>().HasNoKey();
        modelBuilder.HasDbFunction(typeof(E04Context).GetMethod(nameof(GetContactInformation), [typeof(int)])!)
            .HasName("ufnGetContactInformation").HasSchema("dbo");
        modelBuilder.HasDbFunction(typeof(E04Context).GetMethod(nameof(GetStock), [typeof(int)])!)
            .HasName("ufnGetStock").HasSchema("dbo");
    }
}
