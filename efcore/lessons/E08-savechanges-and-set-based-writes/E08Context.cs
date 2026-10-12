using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E08;

// AdventureWorksContext whose model "forgets" the uSalesOrderHeader trigger: what a model looks
// like when nobody declared HasTrigger. Used by one example to show why the declaration matters.
public class E08Context(DbContextOptions<AdventureWorksContext> options) : AdventureWorksContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SalesOrderHeader>().Metadata.RemoveTrigger("uSalesOrderHeader");
    }
}
