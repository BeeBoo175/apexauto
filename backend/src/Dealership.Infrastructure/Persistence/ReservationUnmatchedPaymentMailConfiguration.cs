using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class ReservationUnmatchedPaymentMailConfiguration : IEntityTypeConfiguration<ReservationUnmatchedPaymentMail>
{
    public void Configure(EntityTypeBuilder<ReservationUnmatchedPaymentMail> builder)
    {
        builder.ToTable("ReservationUnmatchedPaymentMailOutbox", table => table.HasCheckConstraint(
            "CK_ReservationUnmatchedPaymentMailOutbox_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => item.RequestId).IsUnique();
        builder.HasIndex(item => new { item.NextDeliveryAt, item.Id });
        builder.HasOne<ReservationDepositPayment>().WithOne().HasForeignKey<ReservationUnmatchedPaymentMail>(item => item.RequestId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
