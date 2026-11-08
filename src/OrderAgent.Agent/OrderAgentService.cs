using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderAgent.Agent.Abstractions;
using OrderAgent.Agent.Conversations;

namespace OrderAgent.Agent;

/// <summary>
/// Punto de entrada del agente, independiente del canal (WhatsApp, HTTP, otro agente vía A2A...).
/// </summary>
public interface IOrderAgent
{
    Task<string> HandleMessageAsync(string customerId, string? customerName, string text, CancellationToken cancellationToken = default);
}

public sealed partial class OrderAgentService(
    IChatClient chatClient,
    IMenuRepository menu,
    IOrderRepository orders,
    IConversationStore conversations,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    IOptions<OrderAgentOptions> options,
    ILoggerFactory loggerFactory) : IOrderAgent
{
    private static readonly string[] ResetCommands = ["reiniciar", "/reset", "empezar de nuevo"];

    internal const string TechnicalErrorReply =
        "Disculpa, tuve un problema técnico y no pude procesar tu mensaje. ¿Me lo puedes enviar de nuevo en un momento?";

    private readonly ILogger _logger = loggerFactory.CreateLogger<OrderAgentService>();
    private readonly OrderAgentOptions _options = options.Value;

    public async Task<string> HandleMessageAsync(
        string customerId, string? customerName, string text, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();

        var conversation = await conversations.GetAsync(customerId, cancellationToken);
        if (conversation is null)
        {
            conversation = Conversation.Start(customerId, now);
            conversations.Add(conversation);
        }
        else if (conversation.IsExpired(now, TimeSpan.FromMinutes(_options.SessionTimeoutMinutes)))
        {
            conversation.Reset(now);
        }

        if (ResetCommands.Contains(text.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            conversation.Reset(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return "Listo, empecemos de nuevo. ¿Qué te gustaría ordenar?";
        }

        string reply;
        try
        {
            reply = await RunAgentAsync(conversation, customerId, customerName, text, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error al ejecutar el agente para el cliente {CustomerId}", customerId);
            return TechnicalErrorReply;
        }

        conversation.Append(ConversationTurn.User, text, now);
        conversation.Append(ConversationTurn.Assistant, reply, time.GetUtcNow());
        conversation.Trim(_options.MaxHistoryTurns);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return reply;
    }

    private async Task<string> RunAgentAsync(
        Conversation conversation, string customerId, string? customerName, string text, CancellationToken cancellationToken)
    {
        var menuItems = await menu.GetAvailableAsync(cancellationToken);
        var tools = new OrderTools(customerId, menu, orders, unitOfWork, time, _options);

        List<ChatMessage> messages =
        [
            new(ChatRole.System, AgentInstructions.Build(_options, menuItems, customerName, ToLocalTime(time.GetUtcNow()))),
            .. conversation.Turns.Select(t =>
                new ChatMessage(t.Role == ConversationTurn.User ? ChatRole.User : ChatRole.Assistant, t.Text)),
            new(ChatRole.User, text),
        ];

        var agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = "OrderAgent",
                Description = "Toma pedidos de clientes de un restaurante.",
                ChatOptions = new ChatOptions
                {
                    Tools = tools.AsAITools(),
                    Temperature = 0.2f,
                    // Desactiva el modo "thinking" en modelos que lo soportan (p. ej. qwen3): respuestas más rápidas.
                    AdditionalProperties = new AdditionalPropertiesDictionary { ["think"] = false },
                },
            },
            loggerFactory);

        var response = await agent.RunAsync(messages, cancellationToken: cancellationToken);
        return CleanReply(response.Text);
    }

    /// <summary>Quita bloques &lt;think&gt; que algunos modelos locales incluyen en el texto.</summary>
    internal static string CleanReply(string? text)
    {
        var cleaned = ThinkBlock().Replace(text ?? string.Empty, string.Empty).Trim();
        return cleaned.Length == 0 ? "¿Me lo podrías repetir, por favor?" : cleaned;
    }

    private DateTimeOffset ToLocalTime(DateTimeOffset utcNow)
    {
        try
        {
            return TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone));
        }
        catch (TimeZoneNotFoundException)
        {
            return utcNow;
        }
    }

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkBlock();
}
