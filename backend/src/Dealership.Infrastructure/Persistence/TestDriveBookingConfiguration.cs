using Dealership.Domain.Customers;
using Dealership.Domain.TestDrives;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.TestDrives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class TestDriveBookingConfiguration : IEntityTypeConfiguration<TestDriveBooking>, IEntityTypeConfiguration<TestDriveMail>
{
    public void Configure(EntityTypeBuilder<TestDriveBooking> builder)
    {
        builder.ToTable("TestDriveBookings", table =>
        {
            table.HasCheckConstraint("CK_TestDriveBookings_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"CreatedAt\") AND isfinite(\"StartsAt\") AND isfinite(\"EndsAt\") AND \"StartsAt\" > \"CreatedAt\" AND \"EndsAt\" = \"StartsAt\" + interval '30 minutes'");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.CustomerId, item.StartsAt, item.Id });
        builder.HasIndex(item => new { item.BranchId, item.StartsAt, item.Id });
        builder.HasIndex(item => new { item.VehicleId, item.StartsAt, item.Id });
        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(item => item.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ListingPublication>().WithMany()
            .HasForeignKey(item => new { item.PublicationId, item.VehicleId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.VehicleId, item.BranchId })
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<TestDriveMail> builder)
    {
        builder.ToTable("TestDriveMailOutbox", table =>
        {
            table.HasCheckConstraint("CK_TestDriveMailOutbox_Kind", "\"Kind\" IN ('Confirmation', 'Cancellation')");
            table.HasCheckConstraint("CK_TestDriveMailOutbox_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0 AND isfinite(\"NextDeliveryAt\") AND ((\"DeliveryClaimId\" IS NULL) = (\"DeliveryClaimExpiresAt\" IS NULL))");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.RecipientEmail).HasMaxLength(256).IsRequired();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(item => item.CancellationBookingId)
            .HasComputedColumnSql("CASE WHEN \"Kind\" = 'Cancellation' THEN \"BookingId\" END", stored: true);
        builder.HasIndex(item => new { item.BookingId, item.Kind }).IsUnique();
        builder.HasIndex(item => new { item.NextDeliveryAt, item.Id });
        builder.HasOne<TestDriveBooking>().WithMany().HasForeignKey(item => item.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TestDriveCancellation>().WithMany().HasForeignKey(item => item.CancellationBookingId)
            .HasPrincipalKey(item => item.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
