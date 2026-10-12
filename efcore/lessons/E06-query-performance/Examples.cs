using EfCoreLessons.Lab;
using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E06;

// Worked examples for the README's concepts. Run with their SQL:
//   dotnet test --filter E06Examples --logger "console;verbosity=detailed"
public class E06Examples : LabTest
{
    static void Print(object? value) => TestContext.Current.TestOutputHelper?.WriteLine($"{value}");

    public record CachedPlan(string ObjType, int UseCounts);

    // Concept 1: tracking keeps every entity (and a snapshot of it); AsNoTracking keeps nothing.
    [Fact]
    public async Task Tracking_vs_AsNoTracking()
    {
        var tracked = await Db.Products.Where(p => p.ListPrice > 1000).ToListAsync();
        Assert.Equal(tracked.Count, Db.ChangeTracker.Entries().Count());

        var other = NewContext();
        var untracked = await other.Products.AsNoTracking().Where(p => p.ListPrice > 1000).ToListAsync();
        Assert.Equal(tracked.Count, untracked.Count);
        Assert.Empty(other.ChangeTracker.Entries());
        Assert.Equal(Sql.Commands[0], Sql.Commands[1]); // same SQL: tracking is a client-side cost
    }

    // Concept 1: AsNoTracking materializes one object per row; identity resolution shares them.
    [Fact]
    public async Task AsNoTracking_duplicates_identity_resolution_shares()
    {
        var plain = await Db.SalesOrderHeaders.AsNoTracking()
            .Include(h => h.Customer).Where(h => h.CustomerId == 11091).ToListAsync();
        var resolved = await Db.SalesOrderHeaders.AsNoTrackingWithIdentityResolution()
            .Include(h => h.Customer).Where(h => h.CustomerId == 11091).ToListAsync();

        Assert.Equal(28, plain.Select(h => h.Customer).Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Single(resolved.Select(h => h.Customer).Distinct(ReferenceEqualityComparer.Instance));
        Assert.Empty(Db.ChangeTracker.Entries());
    }

    static readonly Func<AdventureWorksContext, decimal, IAsyncEnumerable<Product>> PricierThan =
        EF.CompileAsyncQuery((AdventureWorksContext db, decimal min) => db.Products.Where(p => p.ListPrice > min));

    // Concept 2: a compiled query is translated once and stored in a static field.
    [Fact]
    public async Task Compiled_query_runs_like_any_other()
    {
        var expensive = await PricierThan(Db, 3000m).ToListAsync();
        var cheaper = await PricierThan(Db, 2000m).ToListAsync();

        Assert.True(cheaper.Count > expensive.Count);
        Assert.Equal(Sql.Commands[0], Sql.Commands[1]); // one parameterized SQL text
        Assert.Contains("@min", Sql.All);
    }

    // Concept 3: keyset paging seeks the clustered index; a deep OFFSET scans and discards.
    [Fact]
    public async Task Keyset_seeks_offset_scans()
    {
        var lastSeen = 60000;
        var keyset = Db.SalesOrderHeaders.Where(h => h.SalesOrderId > lastSeen)
            .OrderBy(h => h.SalesOrderId).Take(20).Select(h => h.SalesOrderId);
        var offset = Db.SalesOrderHeaders
            .OrderBy(h => h.SalesOrderId).Skip(15000).Take(20).Select(h => h.SalesOrderId);

        var keysetPlan = await QueryPlan.OperatorsAsync(Db, keyset);
        var offsetPlan = await QueryPlan.OperatorsAsync(Db, offset);
        Print($"keyset: {string.Join(" <- ", keysetPlan)}");
        Print($"offset: {string.Join(" <- ", offsetPlan)}");

        Assert.Contains("Clustered Index Seek", keysetPlan);
        Assert.Contains("Clustered Index Scan", offsetPlan);
        Sql.AssertRoundTrips(0); // QueryPlan compiles the queries without running them
    }

    // Concept 4: streaming holds the connection's only open reader until you finish.
    [Fact]
    public async Task Streaming_holds_the_connection()
    {
        var streamed = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => // "already an open DataReader"
        {
            await foreach (var order in Db.SalesOrderHeaders.Where(h => h.CustomerId == 11091).AsAsyncEnumerable())
            {
                streamed++;
                await Db.Customers.CountAsync(); // a second query while the first is still reading
            }
        });
        Assert.Equal(1, streamed);

        var buffered = await Db.SalesOrderHeaders.Where(h => h.CustomerId == 11091).ToListAsync();
        foreach (var order in buffered)
            await Db.Customers.CountAsync(c => c.CustomerId == order.CustomerId); // fine: the reader is closed
    }

    // Concept 5: .Year on a column is DATEPART(...) on every row: no seek. A date range seeks.
    [Fact]
    public async Task Year_is_not_SARGable()
    {
        var year = 2013;
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);
        var byYear = Db.CurrencyRates.Where(r => r.CurrencyRateDate.Year == year).Select(r => r.CurrencyRateId);
        var byRange = Db.CurrencyRates.Where(r => r.CurrencyRateDate >= start && r.CurrencyRateDate < end).Select(r => r.CurrencyRateId);

        Assert.Contains("DATEPART(year, [c].[CurrencyRateDate])", byYear.ToQueryString());
        Assert.Contains("Index Scan", await QueryPlan.OperatorsAsync(Db, byYear));
        Assert.Contains("Index Seek", await QueryPlan.OperatorsAsync(Db, byRange));
        Assert.Equal(await byYear.CountAsync(), await byRange.CountAsync());
    }

    // Concept 5: any function around the column hides it from the index. StartsWith doesn't.
    [Fact]
    public async Task Functions_on_the_column_block_seeks()
    {
        var prefix = "Sm";
        var startsWith = Db.People.Where(p => p.LastName.StartsWith(prefix)).Select(p => p.BusinessEntityId);
        var substring = Db.People.Where(p => p.LastName.Substring(0, 2) == prefix).Select(p => p.BusinessEntityId);
        var upper = Db.People.Where(p => p.LastName.ToUpper().StartsWith(prefix)).Select(p => p.BusinessEntityId);

        Assert.Contains("Index Seek", await QueryPlan.OperatorsAsync(Db, startsWith)); // LIKE @prefix_startswith
        Assert.Contains("Index Scan", await QueryPlan.OperatorsAsync(Db, substring));  // SUBSTRING([LastName], ...)
        Assert.Contains("Index Scan", await QueryPlan.OperatorsAsync(Db, upper));      // UPPER([LastName]) LIKE ...
        Assert.Equal(await startsWith.CountAsync(), await substring.CountAsync());
    }

    // Concept 6: inlined constants compile one ad hoc plan per value; a parameter shares one plan.
    [Fact]
    public async Task Inlined_constants_pollute_the_plan_cache()
    {
        var tag = "E06-" + Guid.NewGuid().ToString("N");
        for (byte status = 1; status <= 5; status++)
            await Db.SalesOrderHeaders.TagWith($"{tag} constant").CountAsync(h => h.Status == EF.Constant(status));
        for (byte status = 1; status <= 5; status++)
            await Db.SalesOrderHeaders.TagWith($"{tag} parameter").CountAsync(h => h.Status == status);

        async Task<List<CachedPlan>> PlansTagged(string label)
        {
            var pattern = $"%{tag} {label}%";
            return await Db.Database.SqlQuery<CachedPlan>($"""
                SELECT cp.objtype AS ObjType, cp.usecounts AS UseCounts
                FROM sys.dm_exec_cached_plans AS cp
                CROSS APPLY sys.dm_exec_sql_text(cp.plan_handle) AS st
                WHERE st.text LIKE {pattern}
                """).ToListAsync();
        }

        var constantPlans = await PlansTagged("constant");
        var parameterPlans = await PlansTagged("parameter");
        Print($"constants: {string.Join(", ", constantPlans)}");
        Print($"parameter: {string.Join(", ", parameterPlans)}");
        Assert.Equal(5, constantPlans.Count(p => p.ObjType == "Adhoc"));         // one plan per value
        Assert.Equal(new CachedPlan("Prepared", 5), Assert.Single(parameterPlans)); // one plan, used 5 times
    }

    // Concept 7: TagWith adds a comment you can search for in logs, DMVs and Query Store.
    [Fact]
    public async Task TagWith_comments_the_SQL()
    {
        await Db.Products.TagWith("E06: product count for the dashboard").CountAsync();
        await Db.Products.TagWithCallSite().CountAsync();

        Assert.StartsWith("-- E06: product count for the dashboard", Sql.Commands[0]);
        Assert.Contains("Examples.cs:", Sql.Commands[1]); // file and line of the query
    }

    // Concept 8: in a database-first model, HasIndex is metadata the scaffolder copied from the database.
    [Fact]
    public void Scaffolded_indexes_are_model_metadata()
    {
        var person = Db.Model.FindEntityType(typeof(Person))!;
        var indexes = person.GetIndexes().Select(i => $"{i.GetDatabaseName()} ({string.Join(", ", i.Properties.Select(p => p.Name))})").ToList();
        foreach (var index in indexes) Print(index);

        Assert.Contains("IX_Person_LastName_FirstName_MiddleName (LastName, FirstName, MiddleName)", indexes);
        Assert.Equal(0, Sql.Count); // read from the model: no query
    }
}
