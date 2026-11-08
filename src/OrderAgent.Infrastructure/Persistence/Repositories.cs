using Microsoft.EntityFrameworkCore;
using OrderAgent.Agent.Abstractions;
using OrderAgent.Agent.Conversations;
using OrderAgent.Domain.Menu;
using OrderAgent.Domain.Orders;

namespace OrderAgent.Infrastructure.Persistence;

internal sealed class MenuRepository(OrderAgentDbContext db) : IMenuRepository
{
    public async Task<IReadOnlyList<MenuItem>> GetAvailableAsync(CancellationToken cancellationToken = default) =>
        await db.MenuItems.AsNoTracking()
            .Where(m => m.IsAvailable)
            .OrderBy(m => m.Category).ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

    public Task<MenuItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.MenuItems.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
}

internal sealed class OrderRepository(OrderAgentDbContext db) : IOrderRepository
{
    public Task<Order?> GetDraftAsync(string customerId, CancellationToken cancellationToken = default) =>
        db.Orders
            .Where(o => o.CustomerId == customerId && o.Status == OrderStatus.Draft)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Order>> ListAsync(OrderStatus? status, int take, CancellationToken cancellationToken = default)
    {
        var query = db.Orders.AsNoTracking();
        if (status is not null)
            query = query.Where(o => o.Status == status);

        return await query.OrderByDescending(o => o.UpdatedAt).Take(take).ToListAsync(cancellationToken);
    }

    public void Add(Order order) => db.Orders.Add(order);
}

internal sealed class ConversationStore(OrderAgentDbContext db) : IConversationStore
{
    public Task<Conversation?> GetAsync(string customerId, CancellationToken cancellationToken = default) =>
        db.Conversations.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

    public void Add(Conversation conversation) => db.Conversations.Add(conversation);
}
