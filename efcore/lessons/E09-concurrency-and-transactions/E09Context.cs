using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E09;

// AdventureWorksContext with two concurrency tokens:
// - Product.ModifiedDate, the legacy-style token: it only works if every writer changes it.
// - a rowversion column "Version" on ProductCategory, as a shadow property. AdventureWorks has no
//   rowversion columns, so AddVersionColumnAsync adds one inside the test's transaction (DDL is
//   transactional in SQL Server: the rollback removes it again). Call it before touching categories.
public class E09Context(DbContextOptions<AdventureWorksContext> options) : AdventureWorksContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Product>().Property(p => p.ModifiedDate).IsConcurrencyToken();
        modelBuilder.Entity<ProductCategory>().Property<byte[]>("Version").IsRowVersion();
    }

    public static Task AddVersionColumnAsync(DbContext db)
        => db.Database.ExecuteSqlRawAsync("ALTER TABLE Production.ProductCategory ADD Version rowversion");
}

// E09Context with EF's retrying execution strategy switched on.
public class RetryingContext(DbContextOptions<AdventureWorksContext> options) : E09Context(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer(sql => sql.EnableRetryOnFailure());
}
