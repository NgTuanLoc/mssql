using System.Text.RegularExpressions;
using EfCoreLessons.Lab;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EfCoreLessons.E12;

public record SplitRow(string FirstName, string LastName, string ContactEmail, bool IsActive);

// The checks for E12. E12Exercises (yours) and E12Solutions both inherit them.
// Every check starts from an empty EfLab_E12 database, applies YOUR migrations (the ones in
// ExerciseMigrations/, created with `dotnet ef migrations add`) step by step, inserts rows in
// between, and checks the schema and that the data survived. Migrations commit for real, so these
// tests use a real (EfLab_) database instead of a rolled-back transaction.
public abstract class E12Checks : IAsyncLifetime
{
    protected static readonly string Database = LabDatabase.For("E12");

    // The context whose migrations are applied: E12ExerciseContext in Exercises.cs, E12SolutionContext
    // in Solutions.cs. Not an exercise: it's already written for you.
    protected abstract DbContext CreateContext(string connectionString, Func<DbContext, bool, CancellationToken, Task>? seeding = null);

    protected abstract string Ex1();
    protected abstract string Ex2();
    protected abstract string Ex3();
    protected abstract string Ex4();
    protected abstract string Ex5();
    protected abstract string Ch1(DbContext db);
    protected abstract Task<List<string>> Ch2(DbContext db);
    protected abstract Func<DbContext, bool, CancellationToken, Task> Ch3();

    public async ValueTask InitializeAsync() => await LabDatabase.RecreateAsync(Database);

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask; // the next test recreates the database
    }

    DbContext NewContext() => CreateContext(LabConnection.For(Database));

    // The full id (20261012123421_Initial) of the migration you named, or a failure that says what to do.
    static string IdOf(DbContext db, string name)
        => db.Database.GetMigrations().SingleOrDefault(m => m.EndsWith("_" + name, StringComparison.Ordinal))
           ?? throw new InvalidOperationException(
               $"No migration named '{name}' for {db.GetType().Name}. Create it with: dotnet ef migrations add {name} " +
               $"--context {db.GetType().Name} --output-dir lessons/E12-migrations-workflow/ExerciseMigrations");

    async Task MigrateToAsync(string name)
    {
        await using var db = NewContext();
        await db.GetService<IMigrator>().MigrateAsync(IdOf(db, name));
    }

    static async Task ExecuteAsync(string sql)
    {
        await using var connection = await LabConnection.OpenAsync(Database);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    async Task<List<T>> RowsAsync<T>(FormattableString sql)
    {
        await using var db = NewContext();
        return await db.Database.SqlQuery<T>(sql).ToListAsync();
    }

    async Task<List<ColumnInfo>> CustomerColumnsAsync()
    {
        await using var db = NewContext();
        return await new Reference(db).ColumnsAsync("Customers");
    }

    [Exercise]
    public async Task Ex1_InitialMigration()
    {
        await MigrateToAsync(Ex1());

        Assert.Equal(
            [new("Id", "int", null, false), new("FullName", "nvarchar", 100, false), new("Email", "nvarchar", 200, false)],
            await CustomerColumnsAsync());
    }

    [Exercise]
    public async Task Ex2_RenameWithoutLosingData()
    {
        await MigrateToAsync(Ex1());
        await ExecuteAsync("INSERT INTO Customers (FullName, Email) VALUES (N'Ada Lovelace', N'ada@example.com'), (N'Alan Turing', N'alan@example.com')");

        await MigrateToAsync(Ex2());

        var columns = await CustomerColumnsAsync();
        Assert.Contains(new ColumnInfo("ContactEmail", "nvarchar", 320, false), columns);
        Assert.DoesNotContain(columns, c => c.Column == "Email");
        Assert.Equal(["ada@example.com", "alan@example.com"],
            (await RowsAsync<string>($"SELECT ContactEmail AS Value FROM Customers")).Order()); // the data survived
    }

    [Exercise]
    public async Task Ex3_NewRequiredColumnForExistingRows()
    {
        await MigrateToAsync(Ex2());
        await ExecuteAsync("INSERT INTO Customers (FullName, ContactEmail) VALUES (N'Ada Lovelace', N'ada@example.com'), (N'Alan Turing', N'alan@example.com')");

        await MigrateToAsync(Ex3());

        Assert.Contains(new ColumnInfo("IsActive", "bit", null, false), await CustomerColumnsAsync());
        Assert.Equal([true, true], await RowsAsync<bool>($"SELECT IsActive AS Value FROM Customers")); // existing customers stay active
    }

    [Exercise]
    public async Task Ex4_SplitAColumnBothWays()
    {
        await MigrateToAsync(Ex3());
        await ExecuteAsync("INSERT INTO Customers (FullName, ContactEmail, IsActive) VALUES (N'Ada Lovelace', N'ada@example.com', 1), (N'Plato', N'plato@example.com', 0)");

        await MigrateToAsync(Ex4());

        var columns = (await CustomerColumnsAsync()).Select(c => c.Column).ToList();
        Assert.DoesNotContain("FullName", columns);
        Assert.Equal(
            [new SplitRow("Ada", "Lovelace", "ada@example.com", true), new SplitRow("Plato", "", "plato@example.com", false)],
            await RowsAsync<SplitRow>($"SELECT FirstName, LastName, ContactEmail, IsActive FROM Customers ORDER BY ContactEmail"));

        await MigrateToAsync(Ex3()); // Down must put FullName back, from the two halves
        Assert.Equal(["Ada Lovelace", "Plato"], await RowsAsync<string>($"SELECT FullName AS Value FROM Customers ORDER BY ContactEmail"));
    }

    [Exercise]
    public async Task Ex5_SeedReferenceData()
    {
        await MigrateToAsync(Ex5());

        Assert.Equal(["Active", "Suspended"], await RowsAsync<string>($"SELECT Name AS Value FROM CustomerStatus ORDER BY Id"));
    }

    [Exercise]
    public async Task Ch1_IdempotentScript()
    {
        await using var db = NewContext();
        var script = Ch1(db);
        var migrations = db.Database.GetMigrations().ToList();

        Assert.Contains("IF NOT EXISTS", script);
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline).Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
        foreach (var run in new[] { 1, 2 }) // a second run must change nothing and fail nowhere
            foreach (var batch in batches)
                await ExecuteAsync(batch);

        Assert.Equal(migrations, await RowsAsync<string>($"SELECT MigrationId AS Value FROM __EFMigrationsHistory ORDER BY MigrationId"));
    }

    [Exercise]
    public async Task Ch2_PendingMigrations()
    {
        await MigrateToAsync(Ex2());
        await using var db = NewContext();
        var all = db.Database.GetMigrations().ToList();

        var pending = await Ch2(db);

        Assert.Equal(all.SkipWhile(m => !m.EndsWith("_" + Ex2(), StringComparison.Ordinal)).Skip(1).ToList(), pending);
        Assert.NotEmpty(pending);
    }

    [Exercise]
    public async Task Ch3_IdempotentSeeding()
    {
        var seeding = Ch3();

        for (var start = 0; start < 2; start++) // two app starts: seeding runs on every Migrate
        {
            await using var db = CreateContext(LabConnection.For(Database), seeding);
            await db.Database.MigrateAsync();
        }

        Assert.Equal(1, (await RowsAsync<int>($"SELECT COUNT(*) AS Value FROM Customers WHERE ContactEmail = N'store@example.com'")).Single());
    }
}
