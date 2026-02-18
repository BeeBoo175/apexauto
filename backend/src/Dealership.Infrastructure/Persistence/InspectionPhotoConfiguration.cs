using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class InspectionPhotoConfiguration : IEntityTypeConfiguration<InspectionPhoto>
{
    public void Configure(EntityTypeBuilder<InspectionPhoto> builder)
    {
        builder.ToTable("InspectionPhotos", table =>
        {
            table.HasCheckConstraint("CK_InspectionPhotos_Identity",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"ObjectKey\" = 'inspection/photos/' || replace(\"Id\"::text, '-', '')");
            table.HasCheckConstraint("CK_InspectionPhotos_Content",
                "\"ContentType\" IN ('image/jpeg', 'image/png', 'image/webp') AND \"Bytes\" BETWEEN 1 AND 10485760 AND \"Sha256\" ~ '^[0-9a-f]{64}$' AND \"Width\" > 0 AND \"Height\" > 0");
            table.HasCheckConstraint("CK_InspectionPhotos_Point",
                "\"PointCode\" ~ '^(FC|ET|EP|IE|UB)-[0-9]{3}$' AND right(\"PointCode\", 3)::integer BETWEEN 1 AND CASE left(\"PointCode\", 2) WHEN 'FC' THEN 45 WHEN 'ET' THEN 50 WHEN 'EP' THEN 45 WHEN 'IE' THEN 40 WHEN 'UB' THEN 30 END");
            table.HasCheckConstraint("CK_InspectionPhotos_Lifetime",
                "isfinite(\"CreatedAt\") AND isfinite(\"ExpiresAt\") AND \"ExpiresAt\" > \"CreatedAt\" AND (\"AvailableAt\" IS NULL OR (isfinite(\"AvailableAt\") AND \"AvailableAt\" >= \"CreatedAt\" AND \"AvailableAt\" < \"ExpiresAt\"))");
        });
        builder.HasKey(photo => photo.Id);
        builder.Property(photo => photo.Id).ValueGeneratedNever();
        builder.HasOne<InspectionDraft>().WithMany().HasForeignKey(photo => new { photo.InspectionId, photo.BranchId })
            .HasPrincipalKey(draft => new { draft.Id, draft.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(photo => photo.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(photo => photo.PointCode).HasMaxLength(6).IsRequired();
        builder.Property(photo => photo.ObjectKey).HasMaxLength(100).IsRequired();
        builder.Property(photo => photo.ContentType).HasMaxLength(32).IsRequired();
        builder.Property(photo => photo.Sha256).HasMaxLength(64).IsRequired();
        builder.HasIndex(photo => new { photo.InspectionId, photo.PointCode, photo.CreatedAt, photo.Id });
        builder.HasIndex(photo => photo.ExpiresAt);
    }
}
