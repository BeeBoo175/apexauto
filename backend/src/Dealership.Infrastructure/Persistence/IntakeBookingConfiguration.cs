using Dealership.Domain.Branches;
using Dealership.Domain.Customers;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Intake;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class IntakeBookingConfiguration : IEntityTypeConfiguration<IntakeAppointment>,
    IEntityTypeConfiguration<SellerValuation>, IEntityTypeConfiguration<AppointmentPhoto>,
    IEntityTypeConfiguration<AppointmentConfirmation>
{
    public void Configure(EntityTypeBuilder<IntakeAppointment> builder)
    {
        builder.ToTable("IntakeAppointments", table =>
        {
            table.HasCheckConstraint("CK_IntakeAppointments_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"RegistrationPlate\" ~ '[^[:space:]]' AND \"StartsAt\" > \"CreatedAt\" AND \"EndsAt\" > \"StartsAt\"");
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
    }

    public void Configure(EntityTypeBuilder<SellerValuation> builder)
    {
        builder.ToTable("SellerValuations", table =>
        {
            table.HasCheckConstraint("CK_SellerValuations_Shape",
                "\"Make\" ~ '[^[:space:]]' AND \"Model\" ~ '[^[:space:]]' AND \"Trim\" ~ '[^[:space:]]' AND \"ManufactureYear\" > 0 AND \"ManufactureYear\" <= \"ValuationYear\" AND \"MileageKilometres\" >= 0 AND \"Minimum\" >= 0 AND \"Maximum\" >= \"Minimum\" AND \"Currency\" = 'MYR'");
        });
        builder.HasKey(item => new { item.AppointmentId, item.OwnerId });
        builder.Property(item => item.AppointmentId).ValueGeneratedNever();
        builder.HasOne<IntakeAppointment>().WithOne().HasForeignKey<SellerValuation>(item => new { item.AppointmentId, item.OwnerId })
            .HasPrincipalKey<IntakeAppointment>(item => new { item.Id, item.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Make).HasMaxLength(60).IsRequired();
        builder.Property(item => item.Model).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Trim).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        builder.Property(item => item.Minimum).HasPrecision(18, 2);
        builder.Property(item => item.Maximum).HasPrecision(18, 2);
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

    public void Configure(EntityTypeBuilder<AppointmentConfirmation> builder)
    {
        builder.ToTable("AppointmentConfirmations", table =>
        {
            table.HasCheckConstraint("CK_AppointmentConfirmations_Shape", "\"RecipientEmail\" ~ '[^[:space:]]' AND \"DeliveryAttempts\" >= 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<IntakeAppointment>().WithOne().HasForeignKey<AppointmentConfirmation>(item => item.Id).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.RecipientEmail).HasMaxLength(256).IsRequired();
        builder.HasIndex(item => new { item.NextDeliveryAt, item.Id });
    }
}
