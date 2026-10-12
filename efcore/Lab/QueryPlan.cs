using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EfCoreLessons.Lab;

// The estimated execution plan SQL Server would use for a query, without running the query.
// Uses the context's own connection and transaction; these commands are not counted in Sql.
public static class QueryPlan
{
    // The plan's physical operators in plan order, e.g. ["Top", "Clustered Index Seek"].
    public static async Task<List<string>> OperatorsAsync(DbContext db, IQueryable query)
    {
        var plan = XDocument.Parse(await ShowplanXmlAsync(db, query.ToQueryString()));
        return plan.Descendants()
            .Select(e => (string?)e.Attribute("PhysicalOp"))
            .OfType<string>()
            .ToList();
    }

    static async Task<string> ShowplanXmlAsync(DbContext db, string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();

        command.CommandText = "SET SHOWPLAN_XML ON";
        await command.ExecuteNonQueryAsync();
        try
        {
            // ToQueryString turns the parameters into DECLAREd local variables, whose values the
            // optimizer doesn't sniff: the plan is compiled for "some value". Good enough to tell a
            // seek from a scan; a real execution's sniffed plan can still differ.
            command.CommandText = sql;
            return (string)(await command.ExecuteScalarAsync())!;
        }
        finally
        {
            command.CommandText = "SET SHOWPLAN_XML OFF";
            await command.ExecuteNonQueryAsync();
        }
    }
}
