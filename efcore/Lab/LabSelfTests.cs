using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.Lab;

// Pins the harness guarantees every lesson relies on.
public class LabSelfTests : LabTest
{
    [Fact]
    public async Task Sql_counts_Db_and_NewContext_commands_but_not_Reference()
    {
        await Db.Products.CountAsync();
        await NewContext().Products.CountAsync();
        await Reference.Rows<int>($"SELECT COUNT(*) AS Value FROM Production.Product");

        Sql.AssertRoundTrips(2);
    }

    [Fact]
    public async Task AssertRoundTrips_failure_lists_the_captured_sql()
    {
        await Db.Products.CountAsync();

        var failure = Assert.ThrowsAny<Exception>(() => Sql.AssertRoundTrips(0));

        Assert.Contains("Expected 0 round trip(s), got 1.", failure.Message);
        Assert.Contains("FROM [Production].[Product]", failure.Message);
    }

    [Fact]
    public async Task Contexts_share_the_test_transaction()
    {
        var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
        product.Name = "Changed inside the test";
        await Db.SaveChangesAsync();

        var seenByOther = await NewContext().Products
            .Where(p => p.ProductId == 1).Select(p => p.Name).SingleAsync();

        Assert.Equal("Changed inside the test", seenByOther);
    }

    [Fact]
    public async Task Writes_are_rolled_back_when_the_test_ends()
    {
        var lab = new ScratchLab();
        await lab.InitializeAsync();
        await lab.RenameProduct1("Never committed");
        await lab.DisposeAsync();

        await using var connection = await LabConnection.OpenAsync(LabConnection.AdventureWorks);
        await using var command = new SqlCommand("SELECT Name FROM Production.Product WHERE ProductID = 1", connection);

        Assert.Equal("Adjustable Race", await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Commands_throw_after_the_test_transaction_was_rolled_back_on_the_server()
    {
        await Db.Database.ExecuteSqlRawAsync("ROLLBACK");

        // Without the guard this would run in autocommit and could write for real.
        var failure = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Db.Products.CountAsync());

        Assert.Contains("transaction", failure.Message);
    }

    [Fact]
    public async Task QueryPlan_compiles_without_running_or_counting()
    {
        var operators = await QueryPlan.OperatorsAsync(Db, Db.Products.Where(p => p.ProductId == 1));

        Assert.Contains("Clustered Index Seek", operators);
        Assert.Equal(0, Sql.Count);
        Assert.Equal(504, await Db.Products.CountAsync()); // SHOWPLAN is off again: queries run normally
    }

    sealed class ScratchLab : LabTest
    {
        public async Task RenameProduct1(string name)
        {
            var product = await Db.Products.SingleAsync(p => p.ProductId == 1);
            product.Name = name;
            await Db.SaveChangesAsync();
        }
    }
}

// LabConnection's password lookup; needs no database.
public class LabConnectionTests
{
    [Fact]
    public void Password_comes_from_the_nearest_docker_env()
    {
        var root = Directory.CreateTempSubdirectory("lab-env-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, "docker"));
            File.WriteAllText(Path.Combine(root.FullName, "docker", ".env"), "OTHER=1\r\nSA_PASSWORD=p@ss=word!\r\n");
            var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "efcore", "bin", "Debug"));

            Assert.Equal("p@ss=word!", LabConnection.FindSaPassword(nested.FullName)); // CRLF trimmed, '=' kept
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Password_is_null_when_no_docker_env_exists()
    {
        var root = Directory.CreateTempSubdirectory("lab-env-");
        try
        {
            Assert.Null(LabConnection.FindSaPassword(root.FullName));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
