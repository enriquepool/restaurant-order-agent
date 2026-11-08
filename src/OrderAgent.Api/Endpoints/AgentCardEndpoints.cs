using Microsoft.Extensions.Options;
using OrderAgent.Agent;

namespace OrderAgent.Api.Endpoints;

/// <summary>
/// "Tarjeta de presentación" del agente según el protocolo A2A (Agent-to-Agent).
/// Otros agentes la leen para descubrir qué sabe hacer este agente.
/// El endpoint JSON-RPC de A2A se implementará en un cambio posterior.
/// </summary>
public static class AgentCardEndpoints
{
    public static IEndpointRouteBuilder MapAgentCard(this IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/agent-card.json", (HttpRequest request, IOptions<OrderAgentOptions> options) =>
        {
            var baseUrl = $"{request.Scheme}://{request.Host}";
            return Results.Json(new
            {
                protocolVersion = "0.3.0",
                name = "OrderAgent",
                description = $"Toma pedidos para el restaurante {options.Value.RestaurantName}.",
                url = $"{baseUrl}/a2a",
                version = "0.1.0",
                capabilities = new { streaming = false, pushNotifications = false },
                defaultInputModes = new[] { "text/plain" },
                defaultOutputModes = new[] { "text/plain" },
                skills = new[]
                {
                    new
                    {
                        id = "take-order",
                        name = "Tomar pedido",
                        description = "Consulta el menú, arma y confirma pedidos de clientes.",
                        tags = new[] { "restaurant", "orders", "menu" },
                        examples = new[] { "Quiero 2 órdenes de tacos al pastor y una horchata" },
                    },
                },
            });
        }).WithTags("A2A").ExcludeFromDescription();

        return app;
    }
}
