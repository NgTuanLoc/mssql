using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace EfCoreLessons.Lab;

// The code-first lessons' own databases. Every name must start with EfLab_: the guard is what keeps
// a create, drop or migration from ever touching AdventureWorks2022 (or anything else) by mistake.
public static partial class LabDatabase
{
    public const string Prefix = "EfLab_";

    // The database for a lesson, e.g. For("E10") => "EfLab_E10".
    public static string For(string lesson) => Guard(Prefix + lesson);

    // Throws unless `database` is an EfLab_ name made only of letters, digits and underscores.
    public static string Guard(string database)
    {
        if (!SafeName().IsMatch(database))
            throw new InvalidOperationException(
                $"Refusing to create, drop or migrate database '{database}': lab databases must be named " +
                $"{Prefix}<letters, digits or _>. AdventureWorks2022 is never touched this way.");
        return database;
    }

    // Creates the database if it doesn't exist yet. Never drops anything.
    public static Task EnsureExistsAsync(string database)
        => OnMaster($"IF DB_ID(N'{Guard(database)}') IS NULL CREATE DATABASE [{database}]");

    // Drops the database (closing other connections to it) and creates it empty.
    public static async Task RecreateAsync(string database)
    {
        await DropAsync(database);
        await OnMaster($"CREATE DATABASE [{database}]");
    }

    // Drops the database if it exists, closing other connections to it first.
    public static async Task DropAsync(string database)
    {
        Guard(database);
        SqlConnection.ClearAllPools(); // idle pooled connections to it would otherwise slow the drop down
        await OnMaster($"""
            IF DB_ID(N'{database}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{database}];
            END
            """);
        SqlConnection.ClearAllPools(); // pooled connections to the dropped database are dead
    }

    static async Task OnMaster(string sql)
    {
        await using var connection = await LabConnection.OpenAsync("master");
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    [GeneratedRegex("^EfLab_[A-Za-z0-9_]+\\z")]
    private static partial Regex SafeName();
}
