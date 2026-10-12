using EfCoreLessons.Model;
using Microsoft.EntityFrameworkCore;

namespace EfCoreLessons.E05;

// AdventureWorksContext with lazy-loading proxies switched on. Every navigation in Model/ is
// virtual, so touching an unloaded one runs a query: each hidden load shows up in Sql.
public class E05Context(DbContextOptions<AdventureWorksContext> options) : AdventureWorksContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseLazyLoadingProxies();
}
