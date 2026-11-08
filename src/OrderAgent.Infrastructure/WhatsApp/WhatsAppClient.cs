using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrderAgent.Infrastructure.WhatsApp;

public interface IWhatsAppClient
{
    Task SendTextAsync(string to, string text, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default);
}

/// <summary>Envía mensajes por WhatsApp Cloud API (Graph API de Meta).</summary>
internal sealed class WhatsAppClient(HttpClient http, IOptions<WhatsAppOptions> options, ILogger<WhatsAppClient> logger)
    : IWhatsAppClient
{
    /// <summary>WhatsApp rechaza mensajes de texto de más de 4096 caracteres.</summary>
    internal const int MaxTextLength = 4096;

    private readonly WhatsAppOptions _options = options.Value;

    public Task SendTextAsync(string to, string text, CancellationToken cancellationToken = default)
    {
        if (text.Length > MaxTextLength)
            text = text[..(MaxTextLength - 1)] + "…";

        return PostAsync(new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = PhoneNumberNormalizer.ForSending(to),
            type = "text",
            text = new { preview_url = false, body = text },
        }, cancellationToken);
    }

    public Task MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default) =>
        PostAsync(new { messaging_product = "whatsapp", status = "read", message_id = messageId }, cancellationToken);

    private async Task PostAsync(object payload, CancellationToken cancellationToken)
    {
        if (!_options.IsConfiguredForSending)
        {
            logger.LogWarning("WhatsApp no está configurado (falta AccessToken o PhoneNumberId); no se envía el mensaje.");
            return;
        }

        var url = $"{_options.GraphApiBaseUrl.TrimEnd('/')}/{_options.GraphApiVersion}/{_options.PhoneNumberId}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("WhatsApp respondió {StatusCode}: {Body}", (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }
    }
}
