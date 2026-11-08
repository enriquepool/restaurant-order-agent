using OrderAgent.Agent.Abstractions;
using OrderAgent.Agent.Conversations;
using OrderAgent.Domain.Menu;
using OrderAgent.Domain.Orders;

namespace OrderAgent.Tests.Fakes;

internal sealed class InMemoryMenu(params MenuItem[] items) : IMenuRepository
{
    public Task<IReadOnlyList<MenuItem>> GetAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MenuItem>>(items.Where(i => i.IsAvailable).ToList());

    public Task<MenuItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(items.FirstOrDefault(i => i.Id == id));
}

internal sealed class InMemoryOrders : IOrderRepository
{
    public List<Order> Orders { get; } = [];

    public Task<Order?> GetDraftAsync(string customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Orders.LastOrDefault(o => o.CustomerId == customerId && o.Status == OrderStatus.Draft));

    public Task<IReadOnlyList<Order>> ListAsync(OrderStatus? status, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Order>>(Orders.Where(o => status is null || o.Status == status).Take(take).ToList());

    public void Add(Order order) => Orders.Add(order);
}

internal sealed class InMemoryConversations : IConversationStore
{
    public Dictionary<string, Conversation> Items { get; } = [];

    public Task<Conversation?> GetAsync(string customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.GetValueOrDefault(customerId));

    public void Add(Conversation conversation) => Items[conversation.CustomerId] = conversation;
}

internal sealed class CountingUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal static class TestMenu
{
    public static readonly MenuItem Tacos = new(1, "Tacos al pastor", "Orden de 3", "Tacos", 75m);
    public static readonly MenuItem Horchata = new(8, "Agua de horchata", "500 ml", "Bebidas", 30m);
    public static readonly MenuItem Pozole = new(7, "Pozole rojo", "Grande", "Platos fuertes", 130m, isAvailable: false);

    public static InMemoryMenu Create() => new(Tacos, Horchata, Pozole);
}
