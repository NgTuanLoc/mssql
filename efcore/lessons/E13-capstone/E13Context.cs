using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E13;

// One vendor's purchases in one month, with the previous month's total (null for the vendor's first
// month). Keyless: it's a report row, not an entity you save. Challenges 1 and 2 map and query it.
public class VendorMonth
{
    public int VendorId { get; set; }
    public DateTime OrderMonth { get; set; }
    public decimal MonthTotal { get; set; }
    public decimal? PrevMonthTotal { get; set; }
}

// E13's model: the shared AdventureWorks model plus whatever MapVendorMonths adds. E13ExerciseContext
// (Exercises.cs, yours) and E13SolutionContext (Solutions.cs) each map VendorMonth their own way. They
// are different context types, so EF caches a separate model for each and they never mix.
public abstract class E13Context(DbContextOptions<AdventureWorksContext> options) : AdventureWorksContext(options)
{
    public IQueryable<VendorMonth> VendorMonths => Set<VendorMonth>();

    protected abstract void MapVendorMonths(ModelBuilder modelBuilder);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        MapVendorMonths(modelBuilder);
    }
}
