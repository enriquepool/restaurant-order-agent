using OrderAgent.Agent;
using OrderAgent.Domain.Orders;
using OrderAgent.Tests.Fakes;

namespace OrderAgent.Tests.Agent;

public class OrderToolsTests
{
    private readonly InMemoryOrders _orders = new();
    private readonly CountingUnitOfWork _uow = new();

    private OrderTools CreateTools(string customer = "c1") =>
        new(customer, TestMenu.Create(), _orders, _uow, TimeProvider.System, new OrderAgentOptions());

    [Fact]
    public async Task Add_creates_draft_order_and_saves()
    {
        var result = await CreateTools().AddItemToOrderAsync(1, 2, "sin cebolla");

        Assert.StartsWith("OK", result);
        Assert.Contains("$150.00 MXN", result);
        Assert.Equal(OrderStatus.Draft, Assert.Single(_orders.Orders).Status);
        Assert.Equal(1, _uow.SaveCount);
    }

    [Fact]
    public async Task Add_unknown_item_returns_error_without_creating_order()
    {
        var result = await CreateTools().AddItemToOrderAsync(999, 1);

        Assert.StartsWith("ERROR", result);
        Assert.Empty(_orders.Orders);
    }

    [Fact]
    public async Task Add_unavailable_item_returns_domain_error()
    {
        var result = await CreateTools().AddItemToOrderAsync(TestMenu.Pozole.Id, 1);

        Assert.StartsWith("ERROR", result);
        Assert.Contains("no está disponible", result);
    }

    [Fact]
    public async Task Summary_lists_items_and_total()
    {
        var tools = CreateTools();
        await tools.AddItemToOrderAsync(1, 1);
        await tools.AddItemToOrderAsync(8, 2);

        var summary = await tools.GetOrderSummaryAsync();

        Assert.Contains("1 x Tacos al pastor", summary);
        Assert.Contains("2 x Agua de horchata", summary);
        Assert.Contains("TOTAL: $135.00 MXN", summary);
    }

    [Fact]
    public async Task Confirm_closes_order_and_returns_folio()
    {
        var tools = CreateTools();
        await tools.AddItemToOrderAsync(1, 1);

        var result = await tools.ConfirmOrderAsync();

        var order = Assert.Single(_orders.Orders);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Contains(OrderTools.ShortFolio(order.Id), result);
    }

    [Fact]
    public async Task Tools_are_isolated_per_customer()
    {
        await CreateTools("ana").AddItemToOrderAsync(1, 1);

        var otherSummary = await CreateTools("luis").GetOrderSummaryAsync();

        Assert.Contains("no tiene una orden en curso", otherSummary);
    }
}
