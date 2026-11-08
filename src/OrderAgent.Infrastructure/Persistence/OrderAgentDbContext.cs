using Microsoft.EntityFrameworkCore;
using OrderAgent.Agent.Abstractions;
using OrderAgent.Agent.Conversations;
using OrderAgent.Domain.Menu;
using OrderAgent.Domain.Orders;

namespace OrderAgent.Infrastructure.Persistence;

public sealed class OrderAgentDbContext(DbContextOptions<OrderAgentDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Conversation> Conversations => Set<Conversation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderAgentDbContext).Assembly);

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);
}
