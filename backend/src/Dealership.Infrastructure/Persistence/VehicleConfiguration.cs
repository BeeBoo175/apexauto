using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", table =>
        {
            table.HasCheckConstraint("CK_Vehicles_Identity", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Vin\" ~ '^[A-HJ-NPR-Z0-9]{17}$' AND \"RegistrationIdentity\" ~ '[^[:space:]]'");
            table.HasCheckConstraint("CK_Vehicles_State", "\"MileageKilometres\" >= 0 AND \"Status\" IN ('PendingInspection', 'Rejected', 'Available', 'Sold') AND \"ManufactureYear\" > 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.Id, item.IntakeAppointmentId, item.IntakeOwnerId, item.BranchId });
        builder.HasOne<IntakeAppointment>().WithOne().HasForeignKey<Vehicle>(item =>
                new { item.IntakeAppointmentId, item.IntakeOwnerId, item.BranchId })
            .HasPrincipalKey<IntakeAppointment>(item => new { item.Id, item.OwnerId, item.BranchId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.Vin).IsUnique();
        builder.HasIndex(item => item.RegistrationIdentity).IsUnique();
        builder.Property(item => item.Vin).HasMaxLength(17).IsRequired();
        builder.Property(item => item.RegistrationPlate).HasMaxLength(20).IsRequired();
        builder.Property(item => item.RegistrationIdentity).HasMaxLength(20).IsRequired();
        builder.Property(item => item.Make).HasMaxLength(60).IsRequired();
        builder.Property(item => item.Model).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Trim).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
    }
}
