using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace EfCoreLessons.E01;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E01Examples --logger "console;verbosity=detailed"
public class E01Examples : LabTest
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    // Concept 1: hierarchyid/geography columns and two whole tables never made it into the model.
    [Fact]
    public void Model_is_missing_what_the_scaffolder_could_not_map()
    {
        var employee = Db.Model.FindEntityType(typeof(Employee))!;
        Assert.Null(employee.FindProperty("OrganizationNode"));                     // hierarchyid
        Assert.NotNull(employee.FindProperty(nameof(Employee.OrganizationLevel)));  // computed column, still mapped

        var tables = Db.Model.GetEntityTypes().Select(e => e.GetTableName() ?? e.GetViewName()).ToList();
        Assert.DoesNotContain("Document", tables);          // hierarchyid primary key
        Assert.DoesNotContain("ProductDocument", tables);
        Print($"{tables.Count} entity types in the model");
    }

    // Concept 2: ToQueryString renders the SQL, with DECLAREs for parameters, without executing it.
    [Fact]
    public void ToQueryString_shows_sql_without_a_round_trip()
    {
        var customerId = 29825;
        var query = Db.SalesOrderHeaders
            .Where(h => h.CustomerId == customerId)
            .OrderByDescending(h => h.OrderDate)
            .Select(h => new { h.SalesOrderId, h.OrderDate, h.TotalDue });

        Print(query.ToQueryString());
        Assert.Equal(0, Sql.Count);
    }

    // Concept 2: LogTo is EF's built-in logging, the way to see SQL in any app.
    [Fact]
    public async Task LogTo_prints_sql_from_any_context()
    {
        var log = new List<string>();
        var options = new DbContextOptionsBuilder<AdventureWorksContext>()
            .UseSqlServer(Db.Database.GetDbConnection())
            .LogTo(log.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
            .EnableSensitiveDataLogging() // parameter values in the log: development only
            .Options;
        await using var logged = new AdventureWorksContext(options);
        logged.Database.UseTransaction(Db.Database.CurrentTransaction!.GetDbTransaction());

        var minPrice = 3000m;
        await logged.Products.CountAsync(p => p.ListPrice > minPrice);

        Print(string.Join(Environment.NewLine, log));
        Assert.Contains(log, line => line.Contains("@minPrice='3000'"));
    }

    // Concept 2: TagWith prepends a comment you can search for in the plan cache or Query Store.
    [Fact]
    public async Task TagWith_labels_the_sql()
    {
        await Db.Products.TagWith("E01: products over 1000").CountAsync(p => p.ListPrice > 1000);

        Assert.StartsWith("-- E01: products over 1000", Sql.Commands[0]);
    }

    // Concept 3: Take is TOP, Skip+Take is OFFSET/FETCH; EF parameterizes both numbers.
    [Fact]
    public async Task Take_and_Skip_become_TOP_and_OFFSET_FETCH()
    {
        await Db.Products.OrderBy(p => p.ProductId).Take(5).Select(p => p.Name).ToListAsync();
        await Db.Products.OrderBy(p => p.ProductId).Skip(10).Take(5).Select(p => p.Name).ToListAsync();

        Assert.Contains("SELECT TOP(@", Sql.Commands[0]);
        Assert.Contains("OFFSET @", Sql.Commands[1]);
        Assert.Contains("ROWS FETCH NEXT @", Sql.Commands[1]);
    }

    // Concept 3: First reads TOP(1); Single reads TOP(2) so it can throw when a second row exists.
    [Fact]
    public async Task First_reads_one_row_Single_reads_two()
    {
        var mountain200 = Db.Products.Where(p => p.ProductNumber.StartsWith("BK-M68B")).Select(p => p.Name);

        Print(await mountain200.FirstAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => mountain200.SingleAsync());

        Assert.Contains("TOP(1)", Sql.Commands[0]);
        Assert.Contains("TOP(2)", Sql.Commands[1]);
    }

    // Concept 3: Any is EXISTS (stops at the first match), Count is COUNT(*).
    [Fact]
    public async Task Any_is_EXISTS_Count_is_COUNT()
    {
        Assert.True(await Db.Products.AnyAsync(p => p.ListPrice > 3000));
        Assert.Equal(504, await Db.Products.CountAsync());

        Assert.Contains("EXISTS", Sql.Commands[0]);
        Assert.Contains("COUNT(*)", Sql.Commands[1]);
    }

    // Concept 3: a literal pattern is inlined; a variable becomes an escaped LIKE parameter.
    [Fact]
    public async Task String_methods_become_LIKE()
    {
        var part = "Mountain";
        await Db.Products.CountAsync(p => p.Name.Contains("Mountain"));
        await Db.Products.CountAsync(p => p.Name.Contains(part));

        Assert.Contains("LIKE N'%Mountain%'", Sql.Commands[0]);
        Assert.Contains("LIKE @part_contains ESCAPE N'\\'", Sql.Commands[1]);
    }

    // Concept 4: variables become parameters, literals are inlined, EF.Constant/EF.Parameter override.
    [Fact]
    public async Task Variables_are_parameters_literals_are_inlined()
    {
        var minPrice = 1000m;
        await Db.Products.CountAsync(p => p.ListPrice > minPrice);
        await Db.Products.CountAsync(p => p.ListPrice > 1000m);
        await Db.Products.CountAsync(p => p.ListPrice > EF.Constant(minPrice));
        await Db.Products.CountAsync(p => p.ListPrice > EF.Parameter(1000m));

        Assert.Contains("> @minPrice", Sql.Commands[0]);
        Assert.Contains("> 1000.0", Sql.Commands[1]);
        Assert.Contains("> 1000.0", Sql.Commands[2]);
        Assert.Contains("> @p", Sql.Commands[3]);
    }

    // Concept 5: an untranslatable call in Where throws; EF won't silently filter in memory.
    [Fact]
    public async Task Untranslatable_Where_throws()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Db.Products.Where(p => IsShort(p.Name)).ToListAsync());

        Print(error.Message);
        Assert.Equal(0, Sql.Count);
    }

    // Concept 5: in the final Select the same call is fine: EF fetches Name and runs IsShort in .NET.
    [Fact]
    public async Task Untranslatable_final_Select_runs_on_the_client()
    {
        var rows = await Db.Products
            .Where(p => p.ProductSubcategoryId == 1)
            .Select(p => new { p.Name, Short = IsShort(p.Name) })
            .ToListAsync();

        Assert.Equal(32, rows.Count);
        Assert.DoesNotContain("[p].[ProductNumber]", Sql.Commands[0]); // just Name crosses the wire
    }

    static bool IsShort(string name) => name.Length < 20;
}
