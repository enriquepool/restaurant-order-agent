using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderAgent.Infrastructure.WhatsApp;

// Modelo del JSON que Meta envía al webhook. Solo se mapean los campos que usamos.
// Referencia: https://developers.facebook.com/docs/whatsapp/cloud-api/webhooks/payload-examples

public sealed record WhatsAppWebhookPayload(
    [property: JsonPropertyName("object")] string? Object,
    [property: JsonPropertyName("entry")] List<WhatsAppEntry>? Entry)
{
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Extrae los mensajes entrantes con el nombre de perfil del remitente.</summary>
    public IEnumerable<IncomingWhatsAppMessage> GetMessages()
    {
        foreach (var change in Entry?.SelectMany(e => e.Changes ?? []) ?? [])
        {
            var value = change.Value;
            if (change.Field != "messages" || value?.Messages is null) continue;

            foreach (var message in value.Messages)
            {
                var profileName = value.Contacts?.FirstOrDefault(c => c.WaId == message.From)?.Profile?.Name;
                yield return new IncomingWhatsAppMessage(
                    message.Id,
                    message.From,
                    profileName,
                    message.Type,
                    message.Text?.Body);
            }
        }
    }
}

public sealed record WhatsAppEntry(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("changes")] List<WhatsAppChange>? Changes);

public sealed record WhatsAppChange(
    [property: JsonPropertyName("field")] string? Field,
    [property: JsonPropertyName("value")] WhatsAppChangeValue? Value);

public sealed record WhatsAppChangeValue(
    [property: JsonPropertyName("contacts")] List<WhatsAppContact>? Contacts,
    [property: JsonPropertyName("messages")] List<WhatsAppMessage>? Messages);

public sealed record WhatsAppContact(
    [property: JsonPropertyName("wa_id")] string? WaId,
    [property: JsonPropertyName("profile")] WhatsAppProfile? Profile);

public sealed record WhatsAppProfile([property: JsonPropertyName("name")] string? Name);

public sealed record WhatsAppMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] WhatsAppText? Text);

public sealed record WhatsAppText([property: JsonPropertyName("body")] string? Body);

/// <summary>Mensaje entrante ya simplificado, listo para encolar.</summary>
public sealed record IncomingWhatsAppMessage(string MessageId, string From, string? ProfileName, string Type, string? Text);
