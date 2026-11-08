using System.Threading.Channels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderAgent.Agent;

namespace OrderAgent.Infrastructure.WhatsApp;

/// <summary>
/// Cola en memoria. El webhook responde 200 a Meta de inmediato y el agente (que puede tardar
/// varios segundos con un modelo local) procesa el mensaje en segundo plano.
/// </summary>
public sealed class WhatsAppMessageQueue
{
    private readonly Channel<IncomingWhatsAppMessage> _channel =
        Channel.CreateBounded<IncomingWhatsAppMessage>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public bool TryEnqueue(IncomingWhatsAppMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<IncomingWhatsAppMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

internal sealed class WhatsAppMessageProcessor(
    WhatsAppMessageQueue queue,
    IServiceScopeFactory scopeFactory,
    IWhatsAppClient whatsApp,
    IMemoryCache processedMessages,
    ILogger<WhatsAppMessageProcessor> logger) : BackgroundService
{
    internal const string UnsupportedTypeReply =
        "Por ahora solo puedo leer mensajes de texto. ¿Me escribes tu pedido, por favor?";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Un solo lector: los mensajes se procesan en orden. Suficiente para empezar;
        // para escalar se puede particionar por cliente o usar una cola externa.
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error procesando el mensaje de WhatsApp {MessageId}", message.MessageId);
            }
        }
    }

    internal async Task ProcessAsync(IncomingWhatsAppMessage message, CancellationToken cancellationToken)
    {
        // Meta reintenta el webhook si no recibe 200 a tiempo: ignoramos mensajes repetidos.
        if (processedMessages.TryGetValue(message.MessageId, out _))
        {
            logger.LogDebug("Mensaje {MessageId} duplicado; se ignora.", message.MessageId);
            return;
        }
        processedMessages.Set(message.MessageId, true, TimeSpan.FromHours(24));

        await TryMarkAsReadAsync(message.MessageId, cancellationToken);

        if (message.Type != "text" || string.IsNullOrWhiteSpace(message.Text))
        {
            await whatsApp.SendTextAsync(message.From, UnsupportedTypeReply, cancellationToken);
            return;
        }

        logger.LogInformation("Mensaje de {From}: {Text}", message.From, message.Text);

        await using var scope = scopeFactory.CreateAsyncScope();
        var agent = scope.ServiceProvider.GetRequiredService<IOrderAgent>();
        var reply = await agent.HandleMessageAsync(message.From, message.ProfileName, message.Text, cancellationToken);

        logger.LogInformation("Respuesta a {From}: {Reply}", message.From, reply);
        await whatsApp.SendTextAsync(message.From, reply, cancellationToken);
    }

    private async Task TryMarkAsReadAsync(string messageId, CancellationToken cancellationToken)
    {
        try
        {
            await whatsApp.MarkAsReadAsync(messageId, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // No es crítico: si falla, el cliente solo no ve las palomitas azules.
            logger.LogWarning(ex, "No se pudo marcar como leído el mensaje {MessageId}", messageId);
        }
    }
}
