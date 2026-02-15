using Dealership.Domain.Intake;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class AppointmentCancellationConfiguration : IEntityTypeConfiguration<AppointmentCancellation>
{
    public void Configure(EntityTypeBuilder<AppointmentCancellation> builder)
    {
        builder.ToTable("AppointmentCancellations", table =>
        {
            table.HasCheckConstraint("CK_AppointmentCancellations_Retention", "\"PhotosExpireAt\" > \"CancelledAt\"");
        });
        builder.HasKey(item => item.AppointmentId);
        builder.Property(item => item.AppointmentId).ValueGeneratedNever();
        builder.HasOne<IntakeAppointment>().WithOne().HasForeignKey<AppointmentCancellation>(item => new { item.AppointmentId, item.OwnerId })
            .HasPrincipalKey<IntakeAppointment>(item => new { item.Id, item.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.CancelledAt);
    }
}
