using EfCoreLessons.E12.Exercise;
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EfCoreLessons.E12
{
    // E12 exercises. Unlike other lessons, most of the work here happens outside this class: you
    // change the model below (Customer, E12ExerciseContext), run `dotnet ef migrations add`, and edit
    // the migration it generates in ExerciseMigrations/. Each exercise then returns the name of the
    // migration you made. Run:
    //   dotnet test --filter E12Exercises
    // Skipped = not attempted yet, Failed = wrong (read the message), Passed = done.
    // The checks are in Checks.cs. Peek at Solutions.cs (and SolutionMigrations/) only after attempting.
    public class E12Exercises : E12Checks
    {
        protected override DbContext CreateContext(string connectionString, Func<DbContext, bool, CancellationToken, Task>? seeding = null)
        {
            var options = new DbContextOptionsBuilder<E12ExerciseContext>().UseSqlServer(connectionString);
            if (seeding != null) options.UseAsyncSeeding(seeding);
            return new E12ExerciseContext(options.Options);
        }

        // Exercise 1 — The first migration
        // E12ExerciseContext (in Exercises.cs) maps Customer to a Customers table. From efcore/, after
        // `dotnet tool restore`, create its first migration:
        //   dotnet ef migrations add Initial --context E12ExerciseContext --output-dir lessons/E12-migrations-workflow/ExerciseMigrations
        // Then return the migration's name.
        protected override string Ex1()
            => throw new NotImplementedException();

        // Exercise 2 — Rename without losing data
        // Rename Customer.Email to ContactEmail and widen it to 320 characters (in the class and in
        // E12ExerciseContext's HasMaxLength), then add a migration for it. Read what was scaffolded before
        // trusting it: existing emails must survive. Return the migration's name.
        protected override string Ex2()
            => throw new NotImplementedException();

        // Exercise 3 — A new required column for existing rows
        // Add `public bool IsActive { get; set; }` to Customer, then add a migration. Every customer
        // that already exists must end up active (IsActive = 1). Return the migration's name.
        protected override string Ex3()
            => throw new NotImplementedException();

        // Exercise 4 — Split a column, both ways
        // Replace Customer.FullName with FirstName and LastName (nvarchar(50) each), then add a migration
        // that fills them from FullName ("Ada Lovelace" -> "Ada" / "Lovelace"; a single word -> FirstName
        // only, LastName empty). Its Down must rebuild FullName from the two. Return the migration's name.
        protected override string Ex4()
            => throw new NotImplementedException();

        // Exercise 5 — Seed reference data
        // Add a CustomerStatus entity (Id, Name nvarchar(20)) to E12ExerciseContext, with rows 1 "Active"
        // and 2 "Suspended" declared in the model with HasData, then add a migration. Return its name.
        protected override string Ex5()
            => throw new NotImplementedException();

        // Challenge 1 — An idempotent script
        // Return a SQL script of all of `db`'s migrations that is safe to run against a database at any
        // migration state, any number of times: what `dotnet ef migrations script --idempotent` writes.
        // Hint: if running it fails, remember SQL Server compiles a whole batch before running any of it,
        // including the parts an IF would skip. Your own migrationBuilder.Sql calls may need fixing.
        protected override string Ch1(DbContext db)
            => throw new NotImplementedException();

        // Challenge 2 — Pending migrations
        // Return the ids of `db`'s migrations that haven't been applied to its database yet, in order.
        protected override Task<List<string>> Ch2(DbContext db)
            => throw new NotImplementedException();

        // Challenge 3 — Idempotent seeding
        // Return an async seeding function (for UseAsyncSeeding) that makes sure a customer with
        // ContactEmail "store@example.com" (FirstName "Store", LastName "Default", active) exists. It runs
        // on every MigrateAsync / EnsureCreatedAsync, so it must never add a second one.
        protected override Func<DbContext, bool, CancellationToken, Task> Ch3()
            => throw new NotImplementedException();
    }
}

namespace EfCoreLessons.E12.Exercise
{
    // YOUR model. Start here and change it as the exercises ask; each change gets a migration.
    public class Customer
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public class E12ExerciseContext(DbContextOptions<E12ExerciseContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers => Set<Customer>();

        protected override void OnModelCreating(ModelBuilder model)
            => model.Entity<Customer>(customer =>
            {
                customer.Property(c => c.FullName).HasMaxLength(100);
                customer.Property(c => c.Email).HasMaxLength(200);
            });
    }

    // Used by `dotnet ef` at design time. migrations add never connects; database update would, to EfLab_E12.
    public class E12ExerciseContextFactory : IDesignTimeDbContextFactory<E12ExerciseContext>
    {
        public E12ExerciseContext CreateDbContext(string[] args)
            => new(new DbContextOptionsBuilder<E12ExerciseContext>()
                .UseSqlServer(LabConnection.For(LabDatabase.For("E12"))).Options);
    }
}
