namespace OrderAgent.Domain.Orders;

/// <summary>
/// Línea de una orden. Guarda una copia del nombre y precio al momento de ordenar,
/// para que un cambio posterior en el menú no altere órdenes existentes.
/// </summary>
public sealed class OrderItem
{
    public Guid Id { get; private set; }
    public int MenuItemId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public string? Notes { get; private set; }

    public decimal Subtotal => UnitPrice * Quantity;

    private OrderItem() { } // EF Core

    internal OrderItem(int menuItemId, string name, decimal unitPrice, int quantity, string? notes)
    {
        Id = Guid.NewGuid();
        MenuItemId = menuItemId;
        Name = name;
        UnitPrice = unitPrice;
        Quantity = quantity;
        Notes = notes;
    }

    internal void ChangeQuantity(int quantity) => Quantity = quantity;
}
