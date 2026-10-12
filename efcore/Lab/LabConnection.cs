using Microsoft.Data.SqlClient;

namespace EfCoreLessons.Lab;

// Connection strings for the mssql-learn container. The sa password comes from the
// SA_PASSWORD environment variable, else from docker/.env (same source as scripts/).
public static class LabConnection
{
    public const string AdventureWorks = "AdventureWorks2022";

    static readonly Lazy<string> SaPassword = new(ReadSaPassword);
    static SqlException? unreachable;

    public static string For(string database) => new SqlConnectionStringBuilder
    {
        DataSource = "localhost,1433",
        InitialCatalog = database,
        UserID = "sa",
        Password = SaPassword.Value,
        TrustServerCertificate = true,
        ConnectTimeout = 5,
    }.ConnectionString;

    // Opens a connection. After the first failure every later call fails at once,
    // instead of each test waiting out its own connect timeout.
    public static async Task<SqlConnection> OpenAsync(string database)
    {
        if (unreachable != null) throw Unreachable(unreachable);

        var connection = new SqlConnection(For(database));
        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch (SqlException ex)
        {
            unreachable = ex;
            await connection.DisposeAsync();
            throw Unreachable(ex);
        }
    }

    // The SA_PASSWORD line of the nearest docker/.env at or above startDirectory, or null.
    internal static string? FindSaPassword(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir != null; dir = dir.Parent)
        {
            var envFile = Path.Combine(dir.FullName, "docker", ".env");
            if (!File.Exists(envFile)) continue;

            var line = File.ReadLines(envFile).FirstOrDefault(l => l.StartsWith("SA_PASSWORD="));
            if (line != null) return line["SA_PASSWORD=".Length..].Trim();
        }
        return null;
    }

    static string ReadSaPassword()
    {
        var fromEnv = Environment.GetEnvironmentVariable("SA_PASSWORD");
        if (!string.IsNullOrEmpty(fromEnv)) return fromEnv;

        return FindSaPassword(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                "SA password not found. Set SA_PASSWORD or create docker/.env from docker/.env.example.");
    }

    static InvalidOperationException Unreachable(SqlException ex) => new(
        $"Cannot connect to SQL Server on localhost,1433: {ex.Message} " +
        "Is the container running? Start it with: docker compose -f docker/docker-compose.yml up -d", ex);
}
