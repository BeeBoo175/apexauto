using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class ReservationRefundMailConfiguration : IEntityTypeConfiguration<ReservationRefundMail>
{
    public void Configure(EntityTypeBuilder<ReservationRefundMail> builder)
    {
        builder.ToTable("ReservationRefundMailOutbox", table => table.HasCheckConstraint("CK_ReservationRefundMailOutbox_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => item.RefundId).IsUnique();
        builder.HasIndex(item => new { item.NextDeliveryAt, item.Id });
        builder.HasOne<ReservationRefund>().WithOne().HasForeignKey<ReservationRefundMail>(item => item.RefundId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
