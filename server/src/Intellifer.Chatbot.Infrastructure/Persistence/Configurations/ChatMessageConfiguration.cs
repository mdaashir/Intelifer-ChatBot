using Intellifer.Chatbot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intellifer.Chatbot.Infrastructure.Persistence.Configurations;

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Content)
            .IsRequired()
            .HasMaxLength(ChatMessage.MaxContentLength);

        builder.Property(message => message.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(message => message.Sender)
            .HasConversion<int>()
            .IsRequired();

        // datetime2 carries no offset, so EF hands the value back with
        // DateTimeKind.Unspecified. Without re-stamping it as UTC the JSON would
        // serialise without the trailing "Z" and every browser would render the
        // timestamp as if it were local time.
        builder.Property(message => message.CreatedAtUtc)
            .HasConversion(
                value => value,
                value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
            .IsRequired();

        // Leads with the equality filter a history request uses and ends with Id,
        // so the same index also satisfies the ORDER BY with no extra sort.
        builder.HasIndex(message => new { message.SessionId, message.UserId, message.Id });

        // Supports the cascade delete when an account is removed.
        builder.HasIndex(message => message.UserId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(message => message.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
