using OrderAgent.Agent;
using OrderAgent.Agent.Abstractions;
using OrderAgent.Domain.Orders;

namespace OrderAgent.Api.Endpoints;

public sealed record ChatRequest(string CustomerId, string Message, string? CustomerName = null);

public sealed record ChatResponse(string Reply);

public sealed record MenuItemDto(int Id, string Name, string Description, string Category, decimal Price);

public sealed record OrderItemDto(int MenuItemId, string Name, int Quantity, decimal UnitPrice, string? Notes, decimal Subtotal);

public sealed record OrderDto(
    Guid Id, string Folio, string CustomerId, OrderStatus Status, decimal Total,
    DateTimeOffset CreatedAt, DateTimeOffset? ConfirmedAt, IReadOnlyList<OrderItemDto> Items);

/// <summary>API HTTP versionada: la usarán apps móviles, web y otros agentes.</summary>
public static class ApiEndpoints
{
    public static IEndpointRouteBuilder MapApiV1(this IEndpointRouteBuilder app, IHostEnvironment environment)
    {
        var v1 = app.MapGroup("/api/v1");

        v1.MapGet("/menu", async (IMenuRepository menu, CancellationToken ct) =>
                (await menu.GetAvailableAsync(ct))
                .Select(m => new MenuItemDto(m.Id, m.Name, m.Description, m.Category, m.Price)))
            .WithTags("Menu")
            .WithSummary("Menú disponible");

        // Estos endpoints aún no tienen autenticación: solo se exponen en Development.
        if (environment.IsDevelopment())
        {
            v1.MapPost("/chat", ChatAsync)
                .WithTags("Chat")
                .WithSummary("Conversa con el agente sin WhatsApp (pruebas locales)");

            v1.MapGet("/orders", async (OrderStatus? status, IOrderRepository orders, CancellationToken ct) =>
                    (await orders.ListAsync(status, take: 50, ct)).Select(ToDto))
                .WithTags("Orders")
                .WithSummary("Órdenes recientes (vista de cocina)");
        }

        return app;
    }

    internal static async Task<IResult> ChatAsync(ChatRequest request, IOrderAgent agent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId) || string.IsNullOrWhiteSpace(request.Message))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = ["customerId y message son obligatorios."],
            });

        var reply = await agent.HandleMessageAsync(request.CustomerId.Trim(), request.CustomerName, request.Message, ct);
        return Results.Ok(new ChatResponse(reply));
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id, OrderTools.ShortFolio(o.Id), o.CustomerId, o.Status, o.Total, o.CreatedAt, o.ConfirmedAt,
        o.Items.Select(i => new OrderItemDto(i.MenuItemId, i.Name, i.Quantity, i.UnitPrice, i.Notes, i.Subtotal)).ToList());
}
