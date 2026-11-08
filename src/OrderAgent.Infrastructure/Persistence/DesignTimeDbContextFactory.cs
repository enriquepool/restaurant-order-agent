using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderAgent.Infrastructure.Persistence;

/// <summary>
/// Usado solo por `dotnet ef` para generar migraciones; no se conecta a la base de datos.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrderAgentDbContext>
{
    public OrderAgentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrderAgentDbContext>()
            .UseNpgsql("Host=localhost;Database=orderagent;Username=orderagent;Password=orderagent")
            .Options;

        return new OrderAgentDbContext(options);
    }
}
