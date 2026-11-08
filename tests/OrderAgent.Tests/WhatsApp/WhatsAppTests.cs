using System.Text;
using System.Text.Json;
using OrderAgent.Infrastructure.WhatsApp;

namespace OrderAgent.Tests.WhatsApp;

public class WhatsAppSignatureValidatorTests
{
    private const string Secret = "app-secret";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"object":"whatsapp_business_account"}""");

    [Fact]
    public void Valid_signature_is_accepted() =>
        Assert.True(WhatsAppSignatureValidator.IsValid(Body, WhatsAppSignatureValidator.Sign(Body, Secret), Secret));

    [Fact]
    public void Signature_with_other_secret_is_rejected() =>
        Assert.False(WhatsAppSignatureValidator.IsValid(Body, WhatsAppSignatureValidator.Sign(Body, "otro"), Secret));

    [Fact]
    public void Tampered_body_is_rejected()
    {
        var signature = WhatsAppSignatureValidator.Sign(Body, Secret);
        var tampered = Encoding.UTF8.GetBytes("""{"object":"hacked"}""");

        Assert.False(WhatsAppSignatureValidator.IsValid(tampered, signature, Secret));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha1=abc")]
    [InlineData("sha256=not-hex")]
    public void Malformed_headers_are_rejected(string? header) =>
        Assert.False(WhatsAppSignatureValidator.IsValid(Body, header, Secret));
}

public class WhatsAppWebhookPayloadTests
{
    // Ejemplo real (simplificado) de lo que envía Meta cuando llega un mensaje de texto.
    internal const string TextMessageJson = """
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "id": "WABA_ID",
            "changes": [{
              "field": "messages",
              "value": {
                "messaging_product": "whatsapp",
                "metadata": { "display_phone_number": "15550001111", "phone_number_id": "123" },
                "contacts": [{ "profile": { "name": "Ana" }, "wa_id": "5215512345678" }],
                "messages": [{
                  "from": "5215512345678",
                  "id": "wamid.ABC",
                  "timestamp": "1762600000",
                  "type": "text",
                  "text": { "body": "Hola, quiero ordenar" }
                }]
              }
            }]
          }]
        }
        """;

    [Fact]
    public void Extracts_text_message_with_profile_name()
    {
        var payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(TextMessageJson, WhatsAppWebhookPayload.JsonOptions)!;

        var message = Assert.Single(payload.GetMessages());
        Assert.Equal("wamid.ABC", message.MessageId);
        Assert.Equal("5215512345678", message.From);
        Assert.Equal("Ana", message.ProfileName);
        Assert.Equal("text", message.Type);
        Assert.Equal("Hola, quiero ordenar", message.Text);
    }

    [Fact]
    public void Status_updates_produce_no_messages()
    {
        const string json = """
            { "object": "whatsapp_business_account", "entry": [{ "changes": [{ "field": "messages",
              "value": { "statuses": [{ "id": "wamid.X", "status": "delivered" }] } }] }] }
            """;
        var payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(json, WhatsAppWebhookPayload.JsonOptions)!;

        Assert.Empty(payload.GetMessages());
    }
}

public class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("5215512345678", "525512345678")] // México: se quita el "1"
    [InlineData("525512345678", "525512345678")]
    [InlineData("+1 (555) 000-1111", "15550001111")]
    [InlineData("34600111222", "34600111222")]
    public void Normalizes_for_sending(string input, string expected) =>
        Assert.Equal(expected, PhoneNumberNormalizer.ForSending(input));
}
