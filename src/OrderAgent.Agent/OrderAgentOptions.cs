using System.Globalization;

namespace OrderAgent.Agent;

public sealed class OrderAgentOptions
{
    public const string SectionName = "OrderAgent";

    public string RestaurantName { get; set; } = "Restaurante Demo";
    public string CurrencySymbol { get; set; } = "$";
    public string CurrencyCode { get; set; } = "MXN";

    /// <summary>Zona horaria del restaurante (IANA, funciona en Windows, macOS y Linux).</summary>
    public string TimeZone { get; set; } = "America/Mexico_City";

    /// <summary>Máximo de mensajes de historial que se envían al modelo.</summary>
    public int MaxHistoryTurns { get; set; } = 20;

    /// <summary>Tras este tiempo sin mensajes, la conversación empieza de cero.</summary>
    public int SessionTimeoutMinutes { get; set; } = 120;

    public string FormatMoney(decimal amount) =>
        $"{CurrencySymbol}{amount.ToString("0.00", CultureInfo.InvariantCulture)} {CurrencyCode}";
}
