using OrderAgent.Domain;
using OrderAgent.Domain.Menu;
using OrderAgent.Domain.Orders;

namespace OrderAgent.Tests.Domain;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly MenuItem Tacos = new(1, "Tacos al pastor", "Orden de 3", "Tacos", 75m);
    private static readonly MenuItem Agua = new(2, "Agua de horchata", "500 ml", "Bebidas", 30m);

    [Fact]
    public void AddItem_uses_price_from_menu_and_computes_total()
    {
        var order = Order.Start("5215512345678", Now);

        order.AddItem(Tacos, 2, null, Now);
        order.AddItem(Agua, 1, null, Now);

        Assert.Equal(2, order.Items.Count);
        Assert.Equal(75m * 2 + 30m, order.Total);
    }

    [Fact]
    public void AddItem_merges_same_item_with_same_notes()
    {
        var order = Order.Start("c1", Now);

        order.AddItem(Tacos, 1, "sin cebolla", Now);
        order.AddItem(Tacos, 2, "SIN CEBOLLA", Now);

        var line = Assert.Single(order.Items);
        Assert.Equal(3, line.Quantity);
    }

    [Fact]
    public void AddItem_keeps_separate_lines_for_different_notes()
    {
        var order = Order.Start("c1", Now);

        order.AddItem(Tacos, 1, "sin cebolla", Now);
        order.AddItem(Tacos, 1, null, Now);

        Assert.Equal(2, order.Items.Count);
    }

    [Fact]
    public void AddItem_rejects_unavailable_item()
    {
        var soldOut = new MenuItem(3, "Pozole", "Rojo", "Platos fuertes", 120m, isAvailable: false);
        var order = Order.Start("c1", Now);

        Assert.Throws<DomainException>(() => order.AddItem(soldOut, 1, null, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Order.MaxQuantityPerItem + 1)]
    public void AddItem_rejects_invalid_quantities(int quantity)
    {
        var order = Order.Start("c1", Now);

        Assert.Throws<DomainException>(() => order.AddItem(Tacos, quantity, null, Now));
    }

    [Fact]
    public void RemoveItem_without_quantity_removes_all_units()
    {
        var order = Order.Start("c1", Now);
        order.AddItem(Tacos, 2, null, Now);
        order.AddItem(Tacos, 1, "con piña", Now);

        order.RemoveItem(Tacos.Id, null, Now);

        Assert.True(order.IsEmpty);
    }

    [Fact]
    public void RemoveItem_with_quantity_decrements()
    {
        var order = Order.Start("c1", Now);
        order.AddItem(Tacos, 3, null, Now);

        order.RemoveItem(Tacos.Id, 2, Now);

        Assert.Equal(1, Assert.Single(order.Items).Quantity);
    }

    [Fact]
    public void RemoveItem_not_in_order_throws()
    {
        var order = Order.Start("c1", Now);

        Assert.Throws<DomainException>(() => order.RemoveItem(Tacos.Id, null, Now));
    }

    [Fact]
    public void Confirm_empty_order_throws()
    {
        var order = Order.Start("c1", Now);

        Assert.Throws<DomainException>(() => order.Confirm(Now));
    }

    [Fact]
    public void Confirmed_order_cannot_be_modified()
    {
        var order = Order.Start("c1", Now);
        order.AddItem(Tacos, 1, null, Now);
        order.Confirm(Now);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(Now, order.ConfirmedAt);
        Assert.Throws<DomainException>(() => order.AddItem(Agua, 1, null, Now));
        Assert.Throws<DomainException>(() => order.Cancel(Now));
    }

    [Fact]
    public void Price_change_in_menu_does_not_affect_existing_order()
    {
        var item = new MenuItem(10, "Quesadilla", "", "Antojitos", 40m);
        var order = Order.Start("c1", Now);
        order.AddItem(item, 1, null, Now);

        // Simula un menú actualizado con otro precio
        var updated = new MenuItem(10, "Quesadilla", "", "Antojitos", 55m);
        order.AddItem(updated, 1, "con chicharrón", Now);

        Assert.Equal(40m + 55m, order.Total);
    }
}
