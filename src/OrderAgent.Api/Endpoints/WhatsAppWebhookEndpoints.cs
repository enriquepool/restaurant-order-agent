using System.Text.Json;
using Microsoft.Extensions.Options;
using OrderAgent.Infrastructure.WhatsApp;

namespace OrderAgent.Api.Endpoints;

/// <summary>
/// Webhook que Meta llama cuando un cliente escribe al número de WhatsApp.
/// </summary>
public static class WhatsAppWebhookEndpoints
{
    public static IEndpointRouteBuilder MapWhatsAppWebhook(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/webhooks/whatsapp").WithTags("WhatsApp").ExcludeFromDescription();
        group.MapGet("/", Verify);
        group.MapPost("/", ReceiveAsync);
        return app;
    }

    /// <summary>
    /// Verificación inicial: al registrar el webhook en Meta, este envía el VerifyToken y espera
    /// que le devolvamos el "challenge".
    /// </summary>
    internal static IResult Verify(HttpRequest request, IOptions<WhatsAppOptions> options, ILoggerFactory loggerFactory)
    {
        var mode = request.Query["hub.mode"].ToString();
        var token = request.Query["hub.verify_token"].ToString();
        var challenge = request.Query["hub.challenge"].ToString();
        var expected = options.Value.VerifyToken;

        if (mode == "subscribe" && !string.IsNullOrEmpty(expected) && token == expected)
        {
            loggerFactory.CreateLogger(nameof(WhatsAppWebhookEndpoints)).LogInformation("Webhook de WhatsApp verificado.");
            return Results.Text(challenge);
        }

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    internal static async Task<IResult> ReceiveAsync(
        HttpRequest request,
        IOptions<WhatsAppOptions> options,
        WhatsAppMessageQueue queue,
        IHostEnvironment environment,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(WhatsAppWebhookEndpoints));

        // La firma se calcula sobre el cuerpo crudo: hay que leerlo antes de deserializar.
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);
        var body = buffer.ToArray();

        var appSecret = options.Value.AppSecret;
        if (string.IsNullOrEmpty(appSecret))
        {
            if (!environment.IsDevelopment())
            {
                logger.LogError("WhatsApp:AppSecret no está configurado; se rechaza el webhook.");
                return Results.Unauthorized();
            }
            logger.LogWarning("WhatsApp:AppSecret no está configurado; se omite la validación de firma (solo en Development).");
        }
        else if (!WhatsAppSignatureValidator.IsValid(body, request.Headers["X-Hub-Signature-256"], appSecret))
        {
            logger.LogWarning("Firma inválida en el webhook de WhatsApp; se rechaza.");
            return Results.Unauthorized();
        }

        WhatsAppWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(body, WhatsAppWebhookPayload.JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "JSON inválido en el webhook de WhatsApp.");
            return Results.BadRequest();
        }

        // Los avisos de estado (entregado, leído) también llegan aquí; GetMessages los ignora.
        foreach (var message in payload?.GetMessages() ?? [])
        {
            if (!queue.TryEnqueue(message))
                logger.LogError("No se pudo encolar el mensaje {MessageId}.", message.MessageId);
        }

        // Responder 200 rápido; si no, Meta reintenta el envío.
        return Results.Ok();
    }
}
