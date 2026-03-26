using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class ReservationMailConfiguration : IEntityTypeConfiguration<ReservationMail>
{
    public void Configure(EntityTypeBuilder<ReservationMail> builder)
    {
        builder.ToTable("ReservationMailOutbox", table => table.HasCheckConstraint("CK_ReservationMailOutbox_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => item.ReservationId).IsUnique();
        builder.HasIndex(item => new { item.NextDeliveryAt, item.Id });
        builder.HasOne<Reservation>().WithOne().HasForeignKey<ReservationMail>(item => item.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
