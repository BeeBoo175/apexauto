using Dealership.Domain.Customers;
using Dealership.Domain.TestDrives;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Inspections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class TestDriveCancellationConfiguration : IEntityTypeConfiguration<TestDriveCancellation>
{
    public void Configure(EntityTypeBuilder<TestDriveCancellation> builder)
    {
        builder.ToTable("TestDriveCancellations", table =>
        {
            table.HasCheckConstraint("CK_TestDriveCancellations_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAt\")");
            table.HasCheckConstraint("CK_TestDriveCancellations_Actor",
                "(\"InspectionRepairRequestId\" IS NULL AND \"Kind\" = 'CustomerCancelled' AND \"StaffId\" IS NULL AND \"Reason\" IS NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NULL AND \"InspectionReopeningId\" IS NULL) OR " +
                "(\"InspectionRepairRequestId\" IS NULL AND \"Kind\" = 'StaffCancelled' AND \"StaffId\" IS NOT NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NULL AND \"Reason\" IS NOT NULL AND " +
                "\"InspectionReopeningId\" IS NULL AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND " +
                "\"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]') OR " +
                "(\"InspectionRepairRequestId\" IS NULL AND \"Kind\" = 'VehicleReserved' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NOT NULL AND \"ListingWithdrawalId\" IS NULL AND " +
                "\"InspectionReopeningId\" IS NULL AND \"Reason\" IS NOT NULL AND \"Reason\" = 'Vehicle reserved') OR " +
                "(\"InspectionRepairRequestId\" IS NULL AND \"Kind\" = 'ListingWithdrawn' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NOT NULL AND " +
                "\"InspectionReopeningId\" IS NULL AND \"Reason\" IS NOT NULL AND \"Reason\" = 'Listing withdrawn') OR " +
                "(\"InspectionRepairRequestId\" IS NULL AND \"Kind\" = 'InspectionReopened' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NULL AND \"ListingWithdrawalId\" IS NULL AND " +
                "\"InspectionReopeningId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND \"Reason\" = 'Vehicle requires reinspection') OR " +
                "(\"Kind\" = 'InspectionRepairRequested' AND \"StaffId\" IS NULL AND \"ReservationId\" IS NULL AND " +
                "\"ListingWithdrawalId\" IS NULL AND \"InspectionReopeningId\" IS NULL AND \"InspectionRepairRequestId\" IS NOT NULL AND " +
                "\"Reason\" IS NOT NULL AND \"Reason\" = 'Vehicle requires repairs')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasMaxLength(32);
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.HasAlternateKey(item => item.BookingId);
        builder.HasOne<TestDriveBooking>().WithOne().HasForeignKey<TestDriveCancellation>(item => new { item.BookingId, item.CustomerId })
            .HasPrincipalKey<TestDriveBooking>(item => new { item.Id, item.CustomerId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(item => item.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.StaffId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Reservation>().WithMany().HasForeignKey(item => item.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ListingWithdrawal>().WithMany().HasForeignKey(item => item.ListingWithdrawalId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionRepairRequestRecord>().WithMany().HasForeignKey(item => item.InspectionRepairRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionReopening>().WithMany().HasForeignKey(item => item.InspectionReopeningId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
