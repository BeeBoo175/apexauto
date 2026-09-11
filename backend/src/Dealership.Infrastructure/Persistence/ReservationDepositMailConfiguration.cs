using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class ReservationDepositMailConfiguration : IEntityTypeConfiguration<ReservationDepositMail>
{
    public void Configure(EntityTypeBuilder<ReservationDepositMail> builder)
    {
        builder.ToTable("ReservationDepositMailOutbox", table => table.HasCheckConstraint("CK_ReservationDepositMailOutbox_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0 AND " +
            "(\"RecipientEmail\" IS NULL OR \"RecipientEmail\" ~ '[^[:space:]]')"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.RecipientEmail).HasMaxLength(256);
        builder.HasIndex(item => item.RequestId).IsUnique();
        builder.HasIndex(item => new { item.NextDeliveryAt, item.Id });
        builder.HasOne<ReservationDepositRequest>().WithOne().HasForeignKey<ReservationDepositMail>(item => item.RequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
