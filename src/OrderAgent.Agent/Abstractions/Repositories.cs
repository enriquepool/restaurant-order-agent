using OrderAgent.Agent.Conversations;
using OrderAgent.Domain.Menu;
using OrderAgent.Domain.Orders;

namespace OrderAgent.Agent.Abstractions;

// Puertos: el agente define QUÉ necesita; la capa de Infrastructure decide CÓMO (Postgres, etc.).

public interface IMenuRepository
{
    Task<IReadOnlyList<MenuItem>> GetAvailableAsync(CancellationToken cancellationToken = default);
    Task<MenuItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}

public interface IOrderRepository
{
    /// <summary>Orden en curso (Draft) del cliente, si existe.</summary>
    Task<Order?> GetDraftAsync(string customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> ListAsync(OrderStatus? status, int take, CancellationToken cancellationToken = default);

    void Add(Order order);
}

public interface IConversationStore
{
    Task<Conversation?> GetAsync(string customerId, CancellationToken cancellationToken = default);

    void Add(Conversation conversation);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
