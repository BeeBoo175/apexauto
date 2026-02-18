using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Inspections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class InspectionPhotoAttachmentConfiguration : IEntityTypeConfiguration<InspectionDraftPhoto>,
    IEntityTypeConfiguration<InspectionPhotoRemoval>
{
    public void Configure(EntityTypeBuilder<InspectionDraftPhoto> builder)
    {
        builder.ToTable("InspectionDraftPhotos");
        builder.HasKey(item => new { item.SaveId, item.PhotoId });
        builder.Property(item => item.PointCode).HasMaxLength(6).IsRequired();
        builder.HasOne<InspectionDraftSave>().WithMany().HasForeignKey(item => new { item.SaveId, item.InspectionId })
            .HasPrincipalKey(save => new { save.Id, save.InspectionId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionPhoto>().WithMany().HasForeignKey(item => new { item.PhotoId, item.InspectionId, item.PointCode })
            .HasPrincipalKey(photo => new { photo.Id, photo.InspectionId, photo.PointCode }).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<InspectionPhotoRemoval> builder)
    {
        builder.ToTable("InspectionPhotoRemovals", table => table.HasCheckConstraint("CK_InspectionPhotoRemovals_Shape",
            "isfinite(\"EligibleAt\") AND isfinite(\"RequestedAt\") AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND " +
            "isfinite(\"NextAttemptAt\") AND (\"DeletedAt\" IS NULL OR (isfinite(\"DeletedAt\") AND \"DeletedAt\" >= \"RequestedAt\")) AND " +
            "((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL)) AND (\"ClaimId\" IS NULL OR " +
            "(\"ClaimId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"ClaimExpiresAt\")))"));
        builder.HasKey(item => item.PhotoId);
        builder.Property(item => item.PhotoId).ValueGeneratedNever();
        builder.HasOne<InspectionPhoto>().WithOne().HasForeignKey<InspectionPhotoRemoval>(item => item.PhotoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.NextAttemptAt, item.PhotoId });
    }
}
