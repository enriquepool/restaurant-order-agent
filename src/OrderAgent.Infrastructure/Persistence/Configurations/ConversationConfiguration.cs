using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderAgent.Agent.Conversations;

namespace OrderAgent.Infrastructure.Persistence.Configurations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");
        builder.HasKey(c => c.CustomerId);
        builder.Property(c => c.CustomerId).HasMaxLength(50);

        // El historial se guarda como jsonb: una sola columna por conversación.
        builder.Property(c => c.Turns)
            .HasColumnType("jsonb")
            .HasConversion(
                turns => JsonSerializer.Serialize(turns, Json),
                json => JsonSerializer.Deserialize<List<ConversationTurn>>(json, Json) ?? new List<ConversationTurn>(),
                new ValueComparer<List<ConversationTurn>>(
                    (a, b) => a!.SequenceEqual(b!),
                    turns => turns.Aggregate(0, (hash, t) => HashCode.Combine(hash, t.GetHashCode())),
                    turns => turns.ToList()));
    }
}
