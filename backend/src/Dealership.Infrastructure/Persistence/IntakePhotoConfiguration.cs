using Dealership.Domain.Customers;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class IntakePhotoConfiguration : IEntityTypeConfiguration<IntakePhoto>
{
    public void Configure(EntityTypeBuilder<IntakePhoto> builder)
    {
        builder.ToTable("IntakePhotos", table =>
        {
            table.HasCheckConstraint("CK_IntakePhotos_Identity",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ObjectKey\" = 'intake/photos/' || replace(\"Id\"::text, '-', '')");
            table.HasCheckConstraint("CK_IntakePhotos_Content",
                "\"Angle\" IN ('Driver', 'Passenger', 'Front', 'Rear') AND \"ContentType\" IN ('image/jpeg', 'image/png', 'image/webp') AND \"Bytes\" BETWEEN 1 AND 10485760 AND \"Sha256\" ~ '^[0-9a-f]{64}$' AND \"Width\" > 0 AND \"Height\" > 0");
            table.HasCheckConstraint("CK_IntakePhotos_Lifetime",
                "\"ExpiresAt\" > \"CreatedAt\" AND (\"AvailableAt\" IS NULL OR (\"AvailableAt\" >= \"CreatedAt\" AND \"AvailableAt\" < \"ExpiresAt\"))");
        });
        builder.HasKey(photo => photo.Id);
        builder.HasAlternateKey(photo => new { photo.Id, photo.OwnerId, photo.Angle });
        builder.Property(photo => photo.Id).ValueGeneratedNever();
        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(photo => photo.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(photo => photo.UploadedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(photo => new { photo.OwnerId, photo.CreatedAt });
        builder.HasIndex(photo => photo.ExpiresAt);
        builder.Property(photo => photo.ObjectKey).HasMaxLength(100).IsRequired();
        builder.Property(photo => photo.Angle).HasConversion<string>().HasMaxLength(16);
        builder.Property(photo => photo.ContentType).HasMaxLength(32).IsRequired();
        builder.Property(photo => photo.Sha256).HasMaxLength(64).IsRequired();
    }
}
