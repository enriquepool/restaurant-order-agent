using OrderAgent.Domain.Menu;

namespace OrderAgent.Domain.Orders;

/// <summary>
/// Orden de un cliente. Todas las reglas de negocio viven aquí; el agente solo las invoca.
/// </summary>
public sealed class Order
{
    public const int MaxQuantityPerItem = 20;
    public const int MaxNotesLength = 200;

    private readonly List<OrderItem> _items = [];

    public Guid Id { get; private set; }

    /// <summary>Identificador del cliente en el canal (en WhatsApp, su número de teléfono).</summary>
    public string CustomerId { get; private set; } = string.Empty;

    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;
    public decimal Total => _items.Sum(i => i.Subtotal);
    public bool IsEmpty => _items.Count == 0;

    private Order() { } // EF Core

    public static Order Start(string customerId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("El cliente es obligatorio.", nameof(customerId));

        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Status = OrderStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public OrderItem AddItem(MenuItem menuItem, int quantity, string? notes, DateTimeOffset now)
    {
        EnsureDraft();

        if (!menuItem.IsAvailable)
            throw new DomainException($"Lo siento, \"{menuItem.Name}\" no está disponible por ahora.");
        if (quantity < 1)
            throw new DomainException("La cantidad debe ser al menos 1.");

        notes = NormalizeNotes(notes);

        // Mismo platillo con las mismas indicaciones: se suma a la línea existente.
        var existing = _items.FirstOrDefault(i =>
            i.MenuItemId == menuItem.Id && string.Equals(i.Notes, notes, StringComparison.OrdinalIgnoreCase));

        var newQuantity = (existing?.Quantity ?? 0) + quantity;
        if (newQuantity > MaxQuantityPerItem)
            throw new DomainException(
                $"El máximo por platillo es {MaxQuantityPerItem}. Para pedidos más grandes, comunícate con el restaurante.");

        if (existing is not null)
        {
            existing.ChangeQuantity(newQuantity);
            Touch(now);
            return existing;
        }

        var item = new OrderItem(menuItem.Id, menuItem.Name, menuItem.Price, quantity, notes);
        _items.Add(item);
        Touch(now);
        return item;
    }

    /// <summary>
    /// Quita un platillo de la orden. Si <paramref name="quantity"/> es null, quita todas sus unidades.
    /// </summary>
    public void RemoveItem(int menuItemId, int? quantity, DateTimeOffset now)
    {
        EnsureDraft();

        var lines = _items.Where(i => i.MenuItemId == menuItemId).ToList();
        if (lines.Count == 0)
            throw new DomainException("Ese platillo no está en tu orden.");

        if (quantity is null)
        {
            _items.RemoveAll(i => i.MenuItemId == menuItemId);
            Touch(now);
            return;
        }

        if (quantity < 1)
            throw new DomainException("La cantidad a quitar debe ser al menos 1.");

        var remaining = quantity.Value;
        // Descuenta empezando por las líneas agregadas al final.
        foreach (var line in Enumerable.Reverse(lines))
        {
            if (remaining == 0) break;

            if (line.Quantity <= remaining)
            {
                remaining -= line.Quantity;
                _items.Remove(line);
            }
            else
            {
                line.ChangeQuantity(line.Quantity - remaining);
                remaining = 0;
            }
        }

        Touch(now);
    }

    public void Confirm(DateTimeOffset now)
    {
        EnsureDraft();
        if (IsEmpty)
            throw new DomainException("Tu orden está vacía. Agrega al menos un platillo antes de confirmar.");

        Status = OrderStatus.Confirmed;
        ConfirmedAt = now;
        Touch(now);
    }

    public void Cancel(DateTimeOffset now)
    {
        EnsureDraft();
        Status = OrderStatus.Cancelled;
        Touch(now);
    }

    private void EnsureDraft()
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Esta orden ya fue cerrada y no se puede modificar.");
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now;

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;

        notes = notes.Trim();
        return notes.Length > MaxNotesLength ? notes[..MaxNotesLength] : notes;
    }
}
