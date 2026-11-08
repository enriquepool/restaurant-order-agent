namespace OrderAgent.Agent.Conversations;

public sealed record ConversationTurn(string Role, string Text, DateTimeOffset At)
{
    public const string User = "user";
    public const string Assistant = "assistant";
}

/// <summary>
/// Historial de la conversación con un cliente. Solo se guardan los mensajes de texto
/// (cliente y agente); el estado real de la orden vive en la tabla de órdenes.
/// </summary>
public sealed class Conversation
{
    public string CustomerId { get; private set; } = string.Empty;
    public List<ConversationTurn> Turns { get; private set; } = [];
    public DateTimeOffset UpdatedAt { get; private set; }

    private Conversation() { } // EF Core

    public static Conversation Start(string customerId, DateTimeOffset now) =>
        new() { CustomerId = customerId, UpdatedAt = now };

    public bool IsExpired(DateTimeOffset now, TimeSpan timeout) => now - UpdatedAt > timeout;

    public void Append(string role, string text, DateTimeOffset now)
    {
        Turns.Add(new ConversationTurn(role, text, now));
        UpdatedAt = now;
    }

    /// <summary>Conserva solo los últimos <paramref name="maxTurns"/> mensajes para no saturar al modelo.</summary>
    public void Trim(int maxTurns)
    {
        if (Turns.Count > maxTurns)
            Turns = Turns.Skip(Turns.Count - maxTurns).ToList();
    }

    public void Reset(DateTimeOffset now)
    {
        Turns = [];
        UpdatedAt = now;
    }
}
