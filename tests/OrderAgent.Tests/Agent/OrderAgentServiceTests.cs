using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderAgent.Agent;
using OrderAgent.Agent.Conversations;
using OrderAgent.Tests.Fakes;

namespace OrderAgent.Tests.Agent;

public class OrderAgentServiceTests
{
    private const string Customer = "5215512345678";

    private readonly InMemoryOrders _orders = new();
    private readonly InMemoryConversations _conversations = new();
    private readonly CountingUnitOfWork _uow = new();

    private OrderAgentService CreateService(IChatClient chatClient) => new(
        chatClient,
        TestMenu.Create(),
        _orders,
        _conversations,
        _uow,
        TimeProvider.System,
        Options.Create(new OrderAgentOptions()),
        NullLoggerFactory.Instance);

    [Fact]
    public async Task Model_tool_call_adds_item_using_menu_price()
    {
        var llm = new ScriptedChatClient(
            _ => ScriptedChatClient.ToolCall("add_item_to_order", new() { ["menuItemId"] = 1, ["quantity"] = 2 }),
            messages =>
            {
                // El resultado de la herramienta debe llegar de vuelta al modelo.
                var toolResult = messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single();
                Assert.Contains("2 x Tacos al pastor", toolResult.Result?.ToString());
                return ScriptedChatClient.Text("¡Listo! Agregué 2 órdenes de tacos al pastor.");
            });

        var reply = await CreateService(llm).HandleMessageAsync(Customer, "Ana", "quiero 2 de pastor");

        Assert.Equal("¡Listo! Agregué 2 órdenes de tacos al pastor.", reply);
        var order = Assert.Single(_orders.Orders);
        Assert.Equal(150m, order.Total);

        var turns = _conversations.Items[Customer].Turns;
        Assert.Equal([ConversationTurn.User, ConversationTurn.Assistant], turns.Select(t => t.Role));
    }

    [Fact]
    public async Task System_prompt_includes_menu_and_tools_are_offered()
    {
        var llm = new ScriptedChatClient(_ => ScriptedChatClient.Text("Hola, ¿qué te sirvo?"));

        await CreateService(llm).HandleMessageAsync(Customer, null, "hola");

        var system = llm.ReceivedCalls[0][0];
        Assert.Equal(ChatRole.System, system.Role);
        Assert.Contains("[id 1] Tacos al pastor: $75.00 MXN", system.Text);
        Assert.DoesNotContain("Pozole", system.Text); // no disponible

        var toolNames = llm.ReceivedOptions[0]!.Tools!.Select(t => t.Name).ToList();
        Assert.Contains("add_item_to_order", toolNames);
        Assert.Contains("confirm_order", toolNames);
    }

    [Fact]
    public async Task History_is_sent_on_following_messages()
    {
        var llm = new ScriptedChatClient(
            _ => ScriptedChatClient.Text("Hola, ¿qué te sirvo?"),
            _ => ScriptedChatClient.Text("Claro."));
        var service = CreateService(llm);

        await service.HandleMessageAsync(Customer, null, "hola");
        await service.HandleMessageAsync(Customer, null, "unos tacos");

        var second = llm.ReceivedCalls[1];
        Assert.Contains(second, m => m.Role == ChatRole.User && m.Text == "hola");
        Assert.Contains(second, m => m.Role == ChatRole.Assistant && m.Text == "Hola, ¿qué te sirvo?");
        Assert.Equal("unos tacos", second[^1].Text);
    }

    [Fact]
    public async Task Reset_command_clears_history_without_calling_model()
    {
        var llm = new ScriptedChatClient(_ => ScriptedChatClient.Text("Hola"));
        var service = CreateService(llm);
        await service.HandleMessageAsync(Customer, null, "hola");

        var reply = await service.HandleMessageAsync(Customer, null, "Reiniciar");

        Assert.Contains("empecemos de nuevo", reply);
        Assert.Empty(_conversations.Items[Customer].Turns);
        Assert.Single(llm.ReceivedCalls);
    }

    [Fact]
    public async Task Model_failure_returns_friendly_message()
    {
        var reply = await CreateService(new ThrowingChatClient()).HandleMessageAsync(Customer, null, "hola");

        Assert.Equal(OrderAgentService.TechnicalErrorReply, reply);
    }

    [Theory]
    [InlineData("<think>razonando...</think>\nHola!", "Hola!")]
    [InlineData("   ", "¿Me lo podrías repetir, por favor?")]
    [InlineData(null, "¿Me lo podrías repetir, por favor?")]
    public void CleanReply_removes_think_blocks(string? input, string expected) =>
        Assert.Equal(expected, OrderAgentService.CleanReply(input));
}
