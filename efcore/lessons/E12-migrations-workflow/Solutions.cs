using EfCoreLessons.E12.Solution;
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EfCoreLessons.E12
{
    // Worked solutions for E12, each followed by a Why note. Run: dotnet test --filter E12Solutions
    // The solution's migrations are in SolutionMigrations/ (generated with dotnet ef, then edited
    // where the Why notes say so).
    public class E12Solutions : E12Checks
    {
        protected override DbContext CreateContext(string connectionString, Func<DbContext, bool, CancellationToken, Task>? seeding = null)
        {
            var options = new DbContextOptionsBuilder<E12SolutionContext>().UseSqlServer(connectionString);
            if (seeding != null) options.UseAsyncSeeding(seeding);
            return new E12SolutionContext(options.Options);
        }

        // Exercise 1 — The first migration
        // E12ExerciseContext (in Exercises.cs) maps Customer to a Customers table. From efcore/, after
        // `dotnet tool restore`, create its first migration:
        //   dotnet ef migrations add Initial --context E12ExerciseContext --output-dir lessons/E12-migrations-workflow/ExerciseMigrations
        // Then return the migration's name.
        protected override string Ex1() => "Initial";
        // Why: `migrations add` compares the model with the last snapshot (none yet) and scaffolds the
        // operations that get from one to the other: CreateTable here. It writes three files: the
        // migration (Up/Down), its Designer.cs (the model at that point) and the context's ModelSnapshot.cs
        // (the model now). The snapshot is what the next migration diffs against, so commit all three.

        // Exercise 2 — Rename without losing data
        // Rename Customer.Email to ContactEmail and widen it to 320 characters (in the class and in
        // E12ExerciseContext's HasMaxLength), then add a migration for it. Read what was scaffolded before
        // trusting it: existing emails must survive. Return the migration's name.
        protected override string Ex2() => "RenameEmail";
        // Why: EF matches a renamed property to its old column only when nothing else about it changed.
        // Rename AND widen, and it scaffolds DropColumn("Email") + AddColumn("ContactEmail") with the
        // warning "An operation was scaffolded that may result in the loss of data". Applied as is, every
        // email is deleted. The fix is to edit the migration into RenameColumn + AlterColumn, and to write
        // Down as the reverse.

        // Exercise 3 — A new required column for existing rows
        // Add `public bool IsActive { get; set; }` to Customer, then add a migration. Every customer
        // that already exists must end up active (IsActive = 1). Return the migration's name.
        protected override string Ex3() => "AddIsActive";
        // Why: a new NOT NULL column needs a value for the rows already there, and EF scaffolds the CLR
        // default: defaultValue: false, so every existing customer silently becomes inactive. Backfill in the
        // migration (migrationBuilder.Sql("UPDATE Customers SET IsActive = 1")), or scaffold
        // defaultValue: true if new rows should default to active too. Either way, decide it explicitly.

        // Exercise 4 — Split a column, both ways
        // Replace Customer.FullName with FirstName and LastName (nvarchar(50) each), then add a migration
        // that fills them from FullName ("Ada Lovelace" -> "Ada" / "Lovelace"; a single word -> FirstName
        // only, LastName empty). Its Down must rebuild FullName from the two. Return the migration's name.
        protected override string Ex4() => "SplitFullName";
        // Why: EF can't know that two new columns come from an old one, so it scaffolds DropColumn first
        // and the names are gone before anything could copy them. Reorder by hand: add the new columns,
        // copy with migrationBuilder.Sql(...), then drop the old one. Down runs when someone rolls back
        // (dotnet ef database update <earlier>), so it needs the same care in reverse.

        // Exercise 5 — Seed reference data
        // Add a CustomerStatus entity (Id, Name nvarchar(20)) to E12ExerciseContext, with rows 1 "Active"
        // and 2 "Suspended" declared in the model with HasData, then add a migration. Return its name.
        protected override string Ex5() => "SeedStatuses";
        // Why: HasData makes the rows part of the model, so the migration contains InsertData, and
        // changing a row later scaffolds UpdateData. It suits fixed reference data with known keys. It
        // can't express "insert if missing" or anything computed at run time. That's UseSeeding (challenge 3).

        // Challenge 1 — An idempotent script
        // Return a SQL script of all of `db`'s migrations that is safe to run against a database at any
        // migration state, any number of times: what `dotnet ef migrations script --idempotent` writes.
        // Hint: if running it fails, remember SQL Server compiles a whole batch before running any of it,
        // including the parts an IF would skip. Your own migrationBuilder.Sql calls may need fixing.
        protected override string Ch1(DbContext db)
            => db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        // Why: each migration in an idempotent script is wrapped in IF NOT EXISTS (SELECT * FROM
        // __EFMigrationsHistory WHERE MigrationId = ...), so applied ones are skipped. That's the script a
        // DBA can review and run at deploy time, without the app's code or connection strings. A
        // migration bundle (`dotnet ef migrations bundle`) does the same job as an executable.

        // Challenge 2 — Pending migrations
        // Return the ids of `db`'s migrations that haven't been applied to its database yet, in order.
        protected override async Task<List<string>> Ch2(DbContext db)
            => [.. await db.Database.GetPendingMigrationsAsync()];
        // Why: EF compares the migrations compiled into the assembly with the rows in
        // __EFMigrationsHistory. Checking this at startup (and failing fast, or alerting) is safer than
        // calling Migrate() from every instance of a web app at once.

        // Challenge 3 — Idempotent seeding
        // Return an async seeding function (for UseAsyncSeeding) that makes sure a customer with
        // ContactEmail "store@example.com" (FirstName "Store", LastName "Default", active) exists. It runs
        // on every MigrateAsync / EnsureCreatedAsync, so it must never add a second one.
        protected override Func<DbContext, bool, CancellationToken, Task> Ch3()
            => async (db, _, cancellationToken) =>
            {
                var customers = db.Set<Customer>();
                if (await customers.AnyAsync(c => c.ContactEmail == "store@example.com", cancellationToken)) return;
                customers.Add(new Customer { FirstName = "Store", LastName = "Default", ContactEmail = "store@example.com", IsActive = true });
                await db.SaveChangesAsync(cancellationToken);
            };
        // Why: UseSeeding/UseAsyncSeeding (EF 9+) run after Migrate and EnsureCreated, with the
        // migration lock held, so concurrent app instances don't seed twice at once. Unlike HasData, the
        // code can query first and decide, which is what "insert if missing" needs. Being called on every
        // start is the point, so the check-then-insert must be idempotent. (Provide UseSeeding as well if
        // anything calls the synchronous Migrate.)
    }
}

namespace EfCoreLessons.E12.Solution
{
    // The solution's model after all five exercises.
    public class Customer
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string ContactEmail { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public class CustomerStatus
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class E12SolutionContext(DbContextOptions<E12SolutionContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers => Set<Customer>();

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<CustomerStatus>(status =>
            {
                status.Property(s => s.Name).HasMaxLength(20);
                status.HasData(new CustomerStatus { Id = 1, Name = "Active" }, new CustomerStatus { Id = 2, Name = "Suspended" });
            });
            model.Entity<Customer>(customer =>
            {
                customer.Property(c => c.FirstName).HasMaxLength(50);
                customer.Property(c => c.LastName).HasMaxLength(50);
                customer.Property(c => c.ContactEmail).HasMaxLength(320);
            });
        }
    }

    // Used by `dotnet ef` at design time. `migrations add` never connects; `migrations remove`, `migrations list` and `database update` connect to EfLab_E12.
    public class E12SolutionContextFactory : IDesignTimeDbContextFactory<E12SolutionContext>
    {
        public E12SolutionContext CreateDbContext(string[] args)
            => new(new DbContextOptionsBuilder<E12SolutionContext>()
                .UseSqlServer(LabConnection.For(LabDatabase.For("E12"))).Options);
    }
}
