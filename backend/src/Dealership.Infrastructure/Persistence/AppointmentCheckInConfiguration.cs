using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Intake;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class AppointmentCheckInConfiguration : IEntityTypeConfiguration<AppointmentCheckIn>,
    IEntityTypeConfiguration<AppointmentCheckInMail>
{
    public void Configure(EntityTypeBuilder<AppointmentCheckIn> builder)
    {
        builder.ToTable("AppointmentCheckIns", table =>
        {
            table.HasCheckConstraint("CK_AppointmentCheckIns_Physical", "\"CustomerPresent\" AND \"IdentityConfirmed\" AND \"OwnershipConfirmed\" AND \"VehicleConfirmed\" AND \"MileageKilometres\" >= 0 AND \"Vin\" ~ '^[A-HJ-NPR-Z0-9]{17}$'");
            table.HasCheckConstraint("CK_AppointmentCheckIns_Notes", $"\"Notes\" IS NULL OR (\"Notes\" ~ '[^[:space:]]' AND length(\"Notes\") <= {CheckInEvidence.NotesMaximumLength})");
            table.HasCheckConstraint("CK_AppointmentCheckIns_Correction",
                "(\"CorrectionRecordedAt\" IS NULL AND \"CorrectionReason\" IS NULL) OR (\"CorrectionRecordedAt\" IS NOT NULL AND \"CorrectionReason\" IS NOT NULL AND \"CorrectionReason\" ~ '[^[:space:]]' AND \"CorrectionReason\" !~ '[[:cntrl:]]')");
        });
        builder.HasKey(item => item.AppointmentId);
        builder.Property(item => item.AppointmentId).ValueGeneratedNever();
        builder.HasOne<IntakeAppointment>().WithOne().HasForeignKey<AppointmentCheckIn>(item =>
                new { item.AppointmentId, item.OwnerId, item.BranchId })
            .HasPrincipalKey<IntakeAppointment>(item => new { item.Id, item.OwnerId, item.BranchId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithOne().HasForeignKey<AppointmentCheckIn>(item =>
                new { item.VehicleId, item.AppointmentId, item.OwnerId, item.BranchId })
            .HasPrincipalKey<Vehicle>(item => new { item.Id, item.IntakeAppointmentId, item.IntakeOwnerId, item.BranchId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.StaffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.BranchId, item.CheckedInAt });
        builder.Property(item => item.Vin).HasMaxLength(17).IsRequired();
        builder.Property(item => item.Notes).HasMaxLength(CheckInEvidence.NotesMaximumLength);
        builder.Property(item => item.CorrectionReason).HasMaxLength(1000);
    }

    public void Configure(EntityTypeBuilder<AppointmentCheckInMail> builder)
    {
        builder.ToTable("AppointmentCheckInMails", table =>
        {
            table.HasCheckConstraint("CK_AppointmentCheckInMails_Shape", "\"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<AppointmentCheckIn>().WithOne().HasForeignKey<AppointmentCheckInMail>(item => item.Id)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.RecipientEmail).HasMaxLength(256).IsRequired();
        builder.HasIndex(item => new { item.NextDeliveryAt, item.Id });
    }
}
