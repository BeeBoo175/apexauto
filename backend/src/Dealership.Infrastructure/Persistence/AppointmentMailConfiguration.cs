using Dealership.Domain.Intake;
using Dealership.Infrastructure.Intake;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class AppointmentMailConfiguration : IEntityTypeConfiguration<AppointmentMail>
{
    public void Configure(EntityTypeBuilder<AppointmentMail> builder)
    {
        builder.ToTable("AppointmentMailOutbox", table =>
        {
            table.HasCheckConstraint("CK_AppointmentMailOutbox_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0");
            table.HasCheckConstraint("CK_AppointmentMailOutbox_Kind", "\"Kind\" IN ('Confirmation', 'Cancellation', 'CheckIn')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(item => item.RecipientEmail).HasMaxLength(256).IsRequired();
        builder.HasIndex(item => new { item.AppointmentId, item.Kind }).IsUnique();
        builder.HasIndex(item => new { item.NextDeliveryAt, item.Id });
        builder.HasOne<IntakeAppointment>().WithMany().HasForeignKey(item => item.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Constructor values order EF inserts; generated keys prevent contradictory parent relationships in SQL.
        builder.Property(item => item.CancellationAppointmentId)
            .HasComputedColumnSql("CASE WHEN \"Kind\" = 'Cancellation' THEN \"AppointmentId\" END", stored: true);
        builder.HasOne<AppointmentCancellation>().WithMany().HasForeignKey(item => item.CancellationAppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.CheckInAppointmentId)
            .HasComputedColumnSql("CASE WHEN \"Kind\" = 'CheckIn' THEN \"AppointmentId\" END", stored: true);
        builder.HasOne<AppointmentCheckIn>().WithMany().HasForeignKey(item => item.CheckInAppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
