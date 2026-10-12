using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E04;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E04Examples --logger "console;verbosity=detailed"
public class E04Examples : LabTest<E04Context>
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    record ColorCount(string? Color, int Products);

    // Concept 1: every {hole} in FromSql becomes a DbParameter.
    [Fact]
    public async Task FromSql_turns_interpolated_values_into_parameters()
    {
        var color = "Red";
        var red = await Db.Products.FromSql($"SELECT * FROM Production.Product WHERE Color = {color}").ToListAsync();

        Assert.Equal(38, red.Count);
        Assert.Contains("WHERE Color = @p0", Sql.All);
    }

    // Concept 1: concatenating input into FromSqlRaw lets the input rewrite the query.
    [Fact]
    public async Task FromSqlRaw_with_concatenation_is_injectable()
    {
        var input = "x' OR 1=1 --";

#pragma warning disable EF1003 // deliberately injectable: this example demonstrates the attack
        var injected = await Db.Products
            .FromSqlRaw("SELECT * FROM Production.Product WHERE Color = '" + input + "'")
            .CountAsync();
#pragma warning restore EF1003
        var safe = await Db.Products
            .FromSql($"SELECT * FROM Production.Product WHERE Color = {input}")
            .CountAsync();

        Assert.Equal(504, injected); // OR 1=1 matched every product
        Assert.Equal(0, safe);       // the whole input was compared as one value
    }

    // Concept 2: LINQ on top of FromSql wraps your SQL in a subquery.
    [Fact]
    public async Task LINQ_composes_over_FromSql_as_a_subquery()
    {
        var color = "Black";
        var names = await Db.Products
            .FromSql($"SELECT * FROM Production.Product WHERE Color = {color}")
            .Where(p => p.ListPrice > 1000)
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToListAsync();

        Print(string.Join(", ", names));
        Assert.Contains("FROM (", Sql.All);
        Assert.Contains("SELECT * FROM Production.Product WHERE Color = @p0", Sql.All);
    }

    // Concept 2: EXEC/WITH are refused by EF; a trailing ORDER BY is refused by SQL Server.
    [Fact]
    public async Task Some_SQL_cannot_be_composed()
    {
        var notComposable = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Db.Database.SqlQuery<ManagerName>($"EXEC dbo.uspGetEmployeeManagers @BusinessEntityID = {100}")
                .OrderBy(m => m.RecursionLevel)
                .ToListAsync());
        Print(notComposable.Message);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Db.Products.FromSql($"SELECT * FROM Production.Product ORDER BY Name")
                .Where(p => p.ListPrice > 0)
                .ToListAsync());
    }

    // Concept 3: SqlQuery<T> for shapes outside the model; scalars need a column named Value.
    [Fact]
    public async Task SqlQuery_maps_unmapped_types_and_scalars()
    {
        var byColor = await Db.Database
            .SqlQuery<ColorCount>($"SELECT Color, COUNT(*) AS Products FROM Production.Product GROUP BY Color")
            .ToListAsync();
        var total = await Db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Production.Product")
            .SingleAsync();

        Assert.Equal(10, byColor.Count); // 9 colors + NULL
        Assert.Equal(504, total);
    }

    // Concept 4: views are keyless: read-only and never tracked.
    [Fact]
    public async Task Views_are_keyless_read_only_entities()
    {
        var top = await Db.VSalesPeople.OrderByDescending(v => v.SalesYtd).FirstAsync();

        Print($"{top.FirstName} {top.LastName}: {top.SalesYtd:C}");
        Assert.Empty(Db.ChangeTracker.Entries());
        Assert.Throws<InvalidOperationException>(() => Db.VSalesPeople.Add(new VSalesPerson()));
    }

    // Concept 5: a mapped TVF is a queryable source; SelectMany over it becomes CROSS APPLY.
    [Fact]
    public async Task Table_valued_function_composes_like_a_table()
    {
        var contacts = await Db.People
            .Where(p => p.BusinessEntityId <= 5)
            .SelectMany(p => Db.GetContactInformation(p.BusinessEntityId), (p, c) => new { p.BusinessEntityId, c.JobTitle })
            .ToListAsync();

        Assert.Equal(5, contacts.Count);
        Assert.Contains("CROSS APPLY [dbo].[ufnGetContactInformation]([p].[BusinessEntityID])", Sql.All);
    }

    // Concept 5: a mapped scalar UDF translates into a function call inside the SQL.
    [Fact]
    public async Task Scalar_function_maps_to_a_CLR_method()
    {
        var stock = await Db.Products
            .Where(p => p.ProductId <= 4)
            .Select(p => new { p.ProductId, Stock = E04Context.GetStock(p.ProductId) })
            .ToListAsync();

        Print(string.Join(", ", stock));
        Assert.Contains("[dbo].[ufnGetStock]([p].[ProductID])", Sql.All);
        Assert.Throws<NotSupportedException>(() => E04Context.GetStock(1)); // never runs in .NET
    }

    // Concept 6: a procedure's rows map by column name; columns T doesn't have are ignored.
    [Fact]
    public async Task Stored_procedure_results_via_SqlQuery()
    {
        var chain = await Db.Database
            .SqlQuery<ManagerName>($"EXEC dbo.uspGetEmployeeManagers @BusinessEntityID = {100}")
            .ToListAsync();

        Print(string.Join(" -> ", chain.OrderBy(m => m.RecursionLevel).Select(m => $"{m.ManagerFirstName} {m.ManagerLastName}")));
        Assert.Equal(3, chain.Count);
    }

    // Concept 6: ExecuteSql returns rows affected, or -1 when the batch uses SET NOCOUNT ON.
    [Fact]
    public async Task ExecuteSql_returns_rows_affected()
    {
        var subcategory = 1;
        var updated = await Db.Database.ExecuteSqlAsync(
            $"UPDATE Production.Product SET ListPrice = ListPrice * 1.10 WHERE ProductSubcategoryID = {subcategory}");
        var viaProc = await Db.Database.ExecuteSqlAsync(
            $"EXEC HumanResources.uspUpdateEmployeePersonalInfo @BusinessEntityID = {1}, @NationalIDNumber = {"295847284"}, @BirthDate = {new DateTime(1969, 1, 29)}, @MaritalStatus = {"S"}, @Gender = {"M"}");

        Assert.Equal(32, updated);
        Assert.Equal(-1, viaProc); // the procedure runs SET NOCOUNT ON
    }
}
