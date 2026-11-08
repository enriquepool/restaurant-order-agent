using Microsoft.Extensions.AI;
using OllamaSharp;

namespace OrderAgent.Api.Llm;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>Proveedor del modelo. Por ahora: "Ollama". Se pueden agregar Claude, OpenAI, etc.</summary>
    public string Provider { get; set; } = "Ollama";

    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen3:8b";

    /// <summary>Los modelos locales en CPU pueden tardar; damos margen amplio.</summary>
    public int TimeoutSeconds { get; set; } = 180;
}

public static class LlmServiceCollectionExtensions
{
    /// <summary>
    /// Registra el <see cref="IChatClient"/> que usa el agente. El agente solo conoce la interfaz,
    /// así que cambiar de proveedor es cuestión de configuración.
    /// </summary>
    public static IServiceCollection AddLlm(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();

        services.AddChatClient(_ => options.Provider.ToLowerInvariant() switch
            {
                "ollama" => CreateOllama(options),
                _ => throw new InvalidOperationException(
                    $"Proveedor de LLM no soportado: '{options.Provider}'. Valores válidos: Ollama."),
            })
            .UseLogging();

        return services;
    }

    private static IChatClient CreateOllama(LlmOptions options)
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(options.Endpoint),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
        };
        return new OllamaApiClient(http, options.Model);
    }
}
