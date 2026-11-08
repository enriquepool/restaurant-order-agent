using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderAgent.Agent.Abstractions;
using OrderAgent.Infrastructure.Persistence;
using OrderAgent.Infrastructure.WhatsApp;

namespace OrderAgent.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "OrderAgentDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión '{ConnectionStringName}'.");

        // Persistencia (PostgreSQL)
        services.AddDbContext<OrderAgentDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<OrderAgentDbContext>());
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IConversationStore, ConversationStore>();

        // WhatsApp Cloud API
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.AddHttpClient<IWhatsAppClient, WhatsAppClient>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddMemoryCache();
        services.AddSingleton<WhatsAppMessageQueue>();
        services.AddHostedService<WhatsAppMessageProcessor>();

        return services;
    }

    /// <summary>Crea o actualiza las tablas de la base de datos (incluye el menú de ejemplo).</summary>
    public static async Task ApplyMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderAgentDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
