using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.AI;
using OrderAgent.Agent.Abstractions;
using OrderAgent.Domain;
using OrderAgent.Domain.Orders;

namespace OrderAgent.Agent;

/// <summary>
/// Herramientas que el LLM puede invocar. Cada instancia está ligada a UN cliente, así el modelo
/// nunca puede tocar la orden de otra persona. Los errores de negocio se devuelven como texto para
/// que el modelo se los explique al cliente.
/// </summary>
public sealed class OrderTools(
    string customerId,
    IMenuRepository menu,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    OrderAgentOptions options)
{
    public IList<AITool> AsAITools() =>
    [
        AIFunctionFactory.Create(GetMenuAsync, "get_menu"),
        AIFunctionFactory.Create(AddItemToOrderAsync, "add_item_to_order"),
        AIFunctionFactory.Create(RemoveItemFromOrderAsync, "remove_item_from_order"),
        AIFunctionFactory.Create(GetOrderSummaryAsync, "get_order_summary"),
        AIFunctionFactory.Create(ConfirmOrderAsync, "confirm_order"),
        AIFunctionFactory.Create(CancelOrderAsync, "cancel_order"),
    ];

    [Description("Devuelve el menú disponible con el id, nombre, precio y descripción de cada platillo.")]
    public async Task<string> GetMenuAsync(CancellationToken cancellationToken = default)
    {
        var items = await menu.GetAvailableAsync(cancellationToken);
        return MenuFormatter.Format(items, options);
    }

    [Description("Agrega un platillo del menú a la orden del cliente.")]
    public async Task<string> AddItemToOrderAsync(
        [Description("Id numérico del platillo, tomado del menú.")] int menuItemId,
        [Description("Cantidad de unidades, entre 1 y 20.")] int quantity,
        [Description("Indicaciones especiales opcionales, por ejemplo 'sin cebolla'.")] string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var menuItem = await menu.GetByIdAsync(menuItemId, cancellationToken);
        if (menuItem is null)
            return $"ERROR: no existe un platillo con id {menuItemId}. Consulta el menú con get_menu.";

        return await ExecuteAsync(async order =>
        {
            var line = order.AddItem(menuItem, quantity, notes, time.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return $"OK: la orden ahora tiene {line.Quantity} x {line.Name}. Total actual: {options.FormatMoney(order.Total)}.";
        }, createIfMissing: true, cancellationToken);
    }

    [Description("Quita un platillo de la orden. Si no se indica cantidad, se quitan todas sus unidades.")]
    public Task<string> RemoveItemFromOrderAsync(
        [Description("Id numérico del platillo a quitar.")] int menuItemId,
        [Description("Unidades a quitar. Omitir para quitarlo por completo.")] int? quantity = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(async order =>
        {
            order.RemoveItem(menuItemId, quantity, time.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return "OK: platillo quitado.\n" + FormatSummary(order);
        }, createIfMissing: false, cancellationToken);

    [Description("Devuelve el detalle de la orden actual del cliente con el total calculado por el sistema.")]
    public Task<string> GetOrderSummaryAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(order => Task.FromResult(FormatSummary(order)), createIfMissing: false, cancellationToken);

    [Description("Confirma la orden y la envía a cocina. Usar SOLO después de que el cliente aceptó explícitamente el resumen.")]
    public Task<string> ConfirmOrderAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async order =>
        {
            order.Confirm(time.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return $"OK: orden confirmada. Folio: {ShortFolio(order.Id)}. Total: {options.FormatMoney(order.Total)}.";
        }, createIfMissing: false, cancellationToken);

    [Description("Cancela la orden en curso del cliente.")]
    public Task<string> CancelOrderAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async order =>
        {
            order.Cancel(time.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return "OK: orden cancelada.";
        }, createIfMissing: false, cancellationToken);

    public static string ShortFolio(Guid orderId) => orderId.ToString("N")[..6].ToUpperInvariant();

    private async Task<string> ExecuteAsync(
        Func<Order, Task<string>> action, bool createIfMissing, CancellationToken cancellationToken)
    {
        var order = await orders.GetDraftAsync(customerId, cancellationToken);
        if (order is null)
        {
            if (!createIfMissing)
                return "El cliente todavía no tiene una orden en curso.";

            order = Order.Start(customerId, time.GetUtcNow());
            orders.Add(order);
        }

        try
        {
            return await action(order);
        }
        catch (DomainException ex)
        {
            return "ERROR: " + ex.Message;
        }
    }

    private string FormatSummary(Order order)
    {
        if (order.IsEmpty)
            return "La orden está vacía.";

        var sb = new StringBuilder("Orden actual:\n");
        foreach (var item in order.Items)
        {
            sb.Append($"- {item.Quantity} x {item.Name} (id {item.MenuItemId})");
            if (item.Notes is not null) sb.Append($" [{item.Notes}]");
            sb.Append($": {options.FormatMoney(item.Subtotal)}\n");
        }

        sb.Append($"TOTAL: {options.FormatMoney(order.Total)}");
        return sb.ToString();
    }
}
