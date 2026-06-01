using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.TestDrives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class TestDriveHandlingConfiguration : IEntityTypeConfiguration<TestDriveStart>,
    IEntityTypeConfiguration<TestDriveOutcome>, IEntityTypeConfiguration<TestDriveOccupation>
{
    public void Configure(EntityTypeBuilder<TestDriveStart> builder)
    {
        builder.ToTable("TestDriveStarts", table =>
        {
            table.HasCheckConstraint("CK_TestDriveStarts_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"CustomerPresent\" AND \"DrivingLicenceChecked\" AND isfinite(\"StartedAt\")");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => item.BookingId).IsUnique();
        builder.HasOne<TestDriveBooking>().WithMany()
            .HasForeignKey(item => new { item.BookingId, item.VehicleId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.VehicleId, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TestDriveAssignment>().WithMany()
            .HasForeignKey(item => new { item.AssignmentId, item.BookingId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BookingId, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.StaffId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<TestDriveOutcome> builder)
    {
        builder.ToTable("TestDriveOutcomes", table =>
        {
            table.HasCheckConstraint("CK_TestDriveOutcomes_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"CustomerPresent\" AND isfinite(\"EndedAt\") AND " +
                "((\"Kind\" = 'Completed' AND \"StartId\" IS NOT NULL AND \"Reason\" IS NULL) OR " +
                "(\"Kind\" = 'NotCompleted' AND \"Reason\" IS NOT NULL))");
            table.HasCheckConstraint("CK_TestDriveOutcomes_Text",
                "(\"Reason\" IS NULL OR (length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]')) AND " +
                "(\"Notes\" IS NULL OR (length(\"Notes\") BETWEEN 1 AND 1000 AND \"Notes\" = btrim(\"Notes\") AND \"Notes\" ~ '[^[:space:]]' AND \"Notes\" !~ '[[:cntrl:]]'))");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasMaxLength(16);
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.Property(item => item.Notes).HasMaxLength(1000);
        builder.HasIndex(item => item.BookingId).IsUnique();
        builder.HasOne<TestDriveBooking>().WithMany().HasForeignKey(item => new { item.BookingId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TestDriveAssignment>().WithMany()
            .HasForeignKey(item => new { item.AssignmentId, item.BookingId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BookingId, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TestDriveStart>().WithMany().HasForeignKey(item => new { item.StartId, item.BookingId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BookingId, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.StaffId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<TestDriveOccupation> builder)
    {
        builder.ToTable("TestDriveOccupations");
        builder.HasKey(item => item.StartId);
        builder.Property(item => item.StartId).ValueGeneratedNever();
        builder.HasIndex(item => item.VehicleId).IsUnique().HasFilter("\"OutcomeId\" IS NULL");
        builder.HasOne<TestDriveStart>().WithOne()
            .HasForeignKey<TestDriveOccupation>(item => new { item.StartId, item.BookingId, item.VehicleId, item.BranchId })
            .HasPrincipalKey<TestDriveStart>(item => new { item.Id, item.BookingId, item.VehicleId, item.BranchId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TestDriveOutcome>().WithMany()
            .HasForeignKey(item => new { item.OutcomeId, item.BookingId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BookingId, item.BranchId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
