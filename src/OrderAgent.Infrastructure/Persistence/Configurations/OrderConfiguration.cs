using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderAgent.Domain.Orders;

namespace OrderAgent.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.CustomerId).HasMaxLength(50).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(o => o.Total);
        builder.Ignore(o => o.IsEmpty);

        builder.HasIndex(o => new { o.CustomerId, o.Status });

        builder.OwnsMany(o => o.Items, item =>
        {
            item.ToTable("order_items");
            item.WithOwner().HasForeignKey("OrderId");
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedNever();
            item.Property(i => i.Name).HasMaxLength(100).IsRequired();
            item.Property(i => i.UnitPrice).HasPrecision(10, 2);
            item.Property(i => i.Notes).HasMaxLength(200);
            item.Ignore(i => i.Subtotal);
        });

        // EF Core usa el campo privado _items para poder agregar/quitar líneas.
        builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
