using Dealership.Domain.Branches;
using Dealership.Domain.Customers;
using Dealership.Domain.Intake;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class IntakeBookingConfiguration : IEntityTypeConfiguration<IntakeAppointment>,
    IEntityTypeConfiguration<AppointmentPhoto>
{
    public void Configure(EntityTypeBuilder<IntakeAppointment> builder)
    {
        builder.ToTable("IntakeAppointments", table =>
        {
            table.HasCheckConstraint("CK_IntakeAppointments_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RegistrationPlate\" ~ '[^[:space:]]' AND \"StartsAt\" > \"CreatedAt\" AND \"EndsAt\" > \"StartsAt\"");
            table.HasCheckConstraint("CK_IntakeAppointments_Cancellation",
                "(\"CancelledAt\" IS NULL AND \"CancellationPhotosExpireAt\" IS NULL) OR (\"CancelledAt\" IS NOT NULL AND \"CancellationPhotosExpireAt\" IS NOT NULL AND \"CancellationPhotosExpireAt\" > \"CancelledAt\")");
            table.HasCheckConstraint("CK_IntakeAppointments_Valuation",
                "\"ValuationMake\" ~ '[^[:space:]]' AND \"ValuationModel\" ~ '[^[:space:]]' AND \"ValuationTrim\" ~ '[^[:space:]]' AND \"ValuationManufactureYear\" > 0 AND \"ValuationManufactureYear\" <= \"ValuationYear\" AND \"ValuationMileageKilometres\" >= 0 AND \"ValuationMinimum\" >= 0 AND \"ValuationMaximum\" >= \"ValuationMinimum\" AND \"ValuationCurrency\" = 'MYR'");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.Id, item.OwnerId });
        builder.HasAlternateKey(item => new { item.Id, item.OwnerId, item.BranchId });
        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.BranchId, item.StartsAt });
        builder.HasIndex(item => new { item.OwnerId, item.StartsAt, item.Id });
        builder.HasIndex(item => new { item.OwnerId, item.CreatedAt });
        builder.Property(item => item.RegistrationPlate).HasMaxLength(20).IsRequired();

        builder.ComplexProperty(item => item.Valuation, valuation =>
        {
            valuation.IsRequired();
            valuation.Property(item => item.Make).HasColumnName("ValuationMake").HasMaxLength(60).IsRequired();
            valuation.Property(item => item.Model).HasColumnName("ValuationModel").HasMaxLength(100).IsRequired();
            valuation.Property(item => item.Trim).HasColumnName("ValuationTrim").HasMaxLength(100).IsRequired();
            valuation.Property(item => item.ManufactureYear).HasColumnName("ValuationManufactureYear");
            valuation.Property(item => item.MileageKilometres).HasColumnName("ValuationMileageKilometres");
            valuation.Property(item => item.Minimum).HasColumnName("ValuationMinimum").HasPrecision(18, 2);
            valuation.Property(item => item.Maximum).HasColumnName("ValuationMaximum").HasPrecision(18, 2);
            valuation.Property(item => item.Currency).HasColumnName("ValuationCurrency").HasMaxLength(3).IsRequired();
            valuation.Property(item => item.ValuationYear).HasColumnName("ValuationYear");
        });
    }

    public void Configure(EntityTypeBuilder<AppointmentPhoto> builder)
    {
        builder.ToTable("AppointmentPhotos", table =>
        {
            table.HasCheckConstraint("CK_AppointmentPhotos_Angle", "\"Angle\" IN ('Driver', 'Passenger', 'Front', 'Rear')");
        });
        builder.HasKey(item => new { item.AppointmentId, item.Angle });
        builder.HasIndex(item => new { item.PhotoId, item.OwnerId, item.Angle }).IsUnique();
        builder.Property(item => item.Angle).HasConversion<string>().HasMaxLength(16);
        builder.HasOne<IntakeAppointment>().WithMany().HasForeignKey(item => new { item.AppointmentId, item.OwnerId })
            .HasPrincipalKey(item => new { item.Id, item.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IntakePhoto>().WithMany().HasForeignKey(item => new { item.PhotoId, item.OwnerId, item.Angle })
            .HasPrincipalKey(item => new { item.Id, item.OwnerId, item.Angle }).OnDelete(DeleteBehavior.Restrict);
    }
}
