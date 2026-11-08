namespace OrderAgent.Domain.Orders;

public enum OrderStatus
{
    /// <summary>El cliente está armando la orden.</summary>
    Draft = 0,

    /// <summary>El cliente confirmó; lista para la cocina.</summary>
    Confirmed = 1,

    /// <summary>El cliente canceló la orden.</summary>
    Cancelled = 2,
}
