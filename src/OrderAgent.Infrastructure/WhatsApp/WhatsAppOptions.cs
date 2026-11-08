namespace OrderAgent.Infrastructure.WhatsApp;

/// <summary>
/// Configuración de WhatsApp Cloud API (Meta). Los valores secretos se guardan con
/// `dotnet user-secrets` o variables de entorno, nunca en appsettings.json.
/// </summary>
public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>Texto libre que tú inventas; Meta lo envía al registrar el webhook.</summary>
    public string VerifyToken { get; set; } = string.Empty;

    /// <summary>App Secret de la app de Meta; sirve para validar que los mensajes vienen de Meta.</summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>Token de acceso para enviar mensajes (el temporal dura 24 h).</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Phone number ID del número de WhatsApp Business (no es el número de teléfono).</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    public string GraphApiVersion { get; set; } = "v23.0";
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com";

    public bool IsConfiguredForSending =>
        !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(PhoneNumberId);
}
