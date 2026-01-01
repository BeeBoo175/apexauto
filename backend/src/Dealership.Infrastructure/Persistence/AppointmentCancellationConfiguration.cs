using Dealership.Domain.Intake;
using Dealership.Infrastructure.Intake;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class AppointmentCancellationConfiguration : IEntityTypeConfiguration<AppointmentCancellation>,
    IEntityTypeConfiguration<AppointmentCancellationMail>
{
    public void Configure(EntityTypeBuilder<AppointmentCancellation> builder)
    {
        builder.HasKey(item => item.AppointmentId);
        builder.Property(item => item.AppointmentId).ValueGeneratedNever();
        builder.HasOne<IntakeAppointment>().WithOne().HasForeignKey<AppointmentCancellation>(item => new { item.AppointmentId, item.OwnerId })
            .HasPrincipalKey<IntakeAppointment>(item => new { item.Id, item.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.CancelledAt);
    }

    public void Configure(EntityTypeBuilder<AppointmentCancellationMail> builder)
    {
        builder.ToTable("AppointmentCancellationMails", table =>
        {
            table.HasCheckConstraint("CK_AppointmentCancellationMails_Shape", "\"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<AppointmentCancellation>().WithOne().HasForeignKey<AppointmentCancellationMail>(item => item.Id)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.RecipientEmail).HasMaxLength(256).IsRequired();
        builder.HasIndex(item => new { item.NextDeliveryAt, item.Id });
    }
}
