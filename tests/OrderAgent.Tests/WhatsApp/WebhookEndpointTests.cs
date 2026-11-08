using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderAgent.Agent;
using OrderAgent.Api.Endpoints;
using OrderAgent.Infrastructure.WhatsApp;

namespace OrderAgent.Tests.WhatsApp;

public class WebhookEndpointTests
{
    private const string Secret = "app-secret";
    private static readonly IOptions<WhatsAppOptions> Options =
        Microsoft.Extensions.Options.Options.Create(new WhatsAppOptions { VerifyToken = "mi-token", AppSecret = Secret });

    [Fact]
    public void Verify_returns_challenge_when_token_matches()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?hub.mode=subscribe&hub.verify_token=mi-token&hub.challenge=12345");

        var result = WhatsAppWebhookEndpoints.Verify(context.Request, Options, NullLoggerFactory.Instance);

        var text = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ContentHttpResult>(result);
        Assert.Equal("12345", text.ResponseContent);
    }

    [Fact]
    public void Verify_rejects_wrong_token()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?hub.mode=subscribe&hub.verify_token=otro&hub.challenge=12345");

        var result = WhatsAppWebhookEndpoints.Verify(context.Request, Options, NullLoggerFactory.Instance);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Receive_with_valid_signature_enqueues_message()
    {
        var queue = new WhatsAppMessageQueue();
        var body = Encoding.UTF8.GetBytes(WhatsAppWebhookPayloadTests.TextMessageJson);
        var request = CreateRequest(body, WhatsAppSignatureValidator.Sign(body, Secret));

        var result = await WhatsAppWebhookEndpoints.ReceiveAsync(request, Options, queue, new TestEnvironment(), NullLoggerFactory.Instance);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok>(result);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await foreach (var message in queue.ReadAllAsync(cts.Token))
        {
            Assert.Equal("wamid.ABC", message.MessageId);
            break;
        }
    }

    [Fact]
    public async Task Receive_with_invalid_signature_is_unauthorized()
    {
        var body = Encoding.UTF8.GetBytes(WhatsAppWebhookPayloadTests.TextMessageJson);
        var request = CreateRequest(body, "sha256=" + new string('0', 64));

        var result = await WhatsAppWebhookEndpoints.ReceiveAsync(
            request, Options, new WhatsAppMessageQueue(), new TestEnvironment(), NullLoggerFactory.Instance);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult>(result);
    }

    private static HttpRequest CreateRequest(byte[] body, string signature)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(body);
        context.Request.Headers["X-Hub-Signature-256"] = signature;
        return context.Request;
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

public class WhatsAppMessageProcessorTests
{
    private readonly RecordingWhatsAppClient _whatsApp = new();
    private readonly EchoAgent _agent = new();

    private WhatsAppMessageProcessor CreateProcessor()
    {
        var services = new ServiceCollection().AddScoped<IOrderAgent>(_ => _agent).BuildServiceProvider();
        return new WhatsAppMessageProcessor(
            new WhatsAppMessageQueue(),
            services.GetRequiredService<IServiceScopeFactory>(),
            _whatsApp,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<WhatsAppMessageProcessor>.Instance);
    }

    [Fact]
    public async Task Text_message_is_answered_by_agent()
    {
        await CreateProcessor().ProcessAsync(new("wamid.1", "5215512345678", "Ana", "text", "hola"), CancellationToken.None);

        Assert.Equal([("5215512345678", "eco: hola")], _whatsApp.Sent);
        Assert.Equal(["wamid.1"], _whatsApp.Read);
    }

    [Fact]
    public async Task Duplicate_message_is_processed_once()
    {
        var processor = CreateProcessor();
        var message = new IncomingWhatsAppMessage("wamid.1", "521", null, "text", "hola");

        await processor.ProcessAsync(message, CancellationToken.None);
        await processor.ProcessAsync(message, CancellationToken.None);

        Assert.Single(_whatsApp.Sent);
        Assert.Equal(1, _agent.Calls);
    }

    [Fact]
    public async Task Non_text_message_gets_fallback_reply()
    {
        await CreateProcessor().ProcessAsync(new("wamid.2", "521", null, "audio", null), CancellationToken.None);

        Assert.Equal(WhatsAppMessageProcessor.UnsupportedTypeReply, Assert.Single(_whatsApp.Sent).Text);
        Assert.Equal(0, _agent.Calls);
    }

    private sealed class RecordingWhatsAppClient : IWhatsAppClient
    {
        public List<(string To, string Text)> Sent { get; } = [];
        public List<string> Read { get; } = [];

        public Task SendTextAsync(string to, string text, CancellationToken cancellationToken = default)
        {
            Sent.Add((to, text));
            return Task.CompletedTask;
        }

        public Task MarkAsReadAsync(string messageId, CancellationToken cancellationToken = default)
        {
            Read.Add(messageId);
            return Task.CompletedTask;
        }
    }

    private sealed class EchoAgent : IOrderAgent
    {
        public int Calls { get; private set; }

        public Task<string> HandleMessageAsync(string customerId, string? customerName, string text, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult("eco: " + text);
        }
    }
}
