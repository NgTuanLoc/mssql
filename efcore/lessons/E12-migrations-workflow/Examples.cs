using EfCoreLessons.E12.Solution;
using EfCoreLessons.Lab;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EfCoreLessons.E12;

// Worked examples for the README's concepts, using the solution's migrations (SolutionMigrations/).
//   dotnet test --filter E12Examples --logger "console;verbosity=detailed"
// Each example starts from an empty EfLab_E12 database.
public class E12Examples : IAsyncLifetime
{
    static readonly string Database = LabDatabase.For("E12");

    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    public async ValueTask InitializeAsync() => await LabDatabase.RecreateAsync(Database);

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    static E12SolutionContext NewContext() => new(new DbContextOptionsBuilder<E12SolutionContext>()
        .UseSqlServer(LabConnection.For(Database)).Options);

    // Concept 2: __EFMigrationsHistory records which migrations a database has.
    [Fact]
    public async Task The_history_table_records_applied_migrations()
    {
        await using var db = NewContext();
        Assert.Equal(5, (await db.Database.GetPendingMigrationsAsync()).Count());

        await db.Database.MigrateAsync();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Print(string.Join("\n", applied));
        Assert.Equal(db.Database.GetMigrations(), applied);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    // Concept 2: migrating to an earlier target runs the Down methods in reverse order.
    [Fact]
    public async Task Migrating_down_runs_Down()
    {
        await using var db = NewContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync();

        await migrator.MigrateAsync(Migration.InitialDatabase); // "0": before the first migration

        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        var tables = await new Reference(db).TablesAsync();
        Assert.Equal(["__EFMigrationsHistory"], tables); // every table a migration created is gone
    }

    // Concept 3: each migration operation becomes provider-specific SQL.
    [Fact]
    public void Operations_become_SQL()
    {
        using var db = NewContext();
        var generator = db.GetService<IMigrationsSqlGenerator>();

        var rename = new RenameColumnOperation { Table = "Customers", Name = "Email", NewName = "ContactEmail" };
        var add = new AddColumnOperation
        {
            Table = "Customers", Name = "IsActive", ClrType = typeof(bool), ColumnType = "bit", IsNullable = false, DefaultValue = false,
        };
        var sql = string.Join("\n", generator.Generate([rename, add]).Select(c => c.CommandText));
        Print(sql);

        Assert.Contains("EXEC sp_rename N'[Customers].[Email]', N'ContactEmail', 'COLUMN';", sql);
        Assert.Contains("ALTER TABLE [Customers] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);", sql);
    }

    // Concept 4: an idempotent script checks the history table before every migration.
    [Fact]
    public void Idempotent_script_guards_every_migration()
    {
        using var db = NewContext();
        var migrator = db.GetService<IMigrator>();

        var plain = migrator.GenerateScript();
        var idempotent = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.DoesNotContain("IF NOT EXISTS", plain);
        foreach (var id in db.Database.GetMigrations())
            Assert.Contains($"WHERE [MigrationId] = N'{id}'", idempotent);
    }

    // Concept 4: a script can cover a range of migrations, e.g. what production still needs.
    [Fact]
    public void Script_for_a_range_of_migrations()
    {
        using var db = NewContext();
        var ids = db.Database.GetMigrations().ToList();

        var script = db.GetService<IMigrator>().GenerateScript(fromMigration: ids[2], toMigration: ids[4]);

        Assert.Contains("FirstName", script);       // SplitFullName (ids[3])
        Assert.Contains("CustomerStatus", script);  // SeedStatuses (ids[4])
        Assert.DoesNotContain("sp_rename", script); // RenameEmail (ids[1]) is before the range
    }

    // Concept 5: EnsureCreated builds today's schema with no history, so migrations can't follow it.
    [Fact]
    public async Task EnsureCreated_and_migrations_dont_mix()
    {
        await LabDatabase.DropAsync(Database); // EnsureCreated creates the database itself
        await using var db = NewContext();
        await db.Database.EnsureCreatedAsync();

        Assert.Equal(5, (await db.Database.GetPendingMigrationsAsync()).Count()); // no history table at all
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
        Print(failure.Message);
        Assert.Contains("There is already an object named 'Customers'", failure.Message);
    }
}
