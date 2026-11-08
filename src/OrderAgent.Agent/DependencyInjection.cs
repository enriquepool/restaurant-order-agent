using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OrderAgent.Agent;

public static class DependencyInjection
{
    /// <summary>
    /// Registra el agente. Requiere que el host registre un <see cref="Microsoft.Extensions.AI.IChatClient"/>
    /// (Ollama, Claude, OpenAI...) y las implementaciones de los repositorios.
    /// </summary>
    public static IServiceCollection AddOrderAgent(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OrderAgentOptions>(configuration.GetSection(OrderAgentOptions.SectionName));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IOrderAgent, OrderAgentService>();
        return services;
    }
}
