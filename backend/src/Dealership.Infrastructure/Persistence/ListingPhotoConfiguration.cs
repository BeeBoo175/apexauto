using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class ListingPhotoConfiguration : IEntityTypeConfiguration<ListingPhoto>, IEntityTypeConfiguration<ListingRevisionPhoto>
{
    public void Configure(EntityTypeBuilder<ListingPhoto> builder)
    {
        builder.ToTable("ListingPhotos", table => table.HasCheckConstraint("CK_ListingPhotos_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
            "\"ContentType\" IN ('image/jpeg', 'image/png', 'image/webp') AND \"Bytes\" BETWEEN 1 AND 10485760 AND " +
            "\"Width\" > 0 AND \"Height\" > 0 AND \"Sha256\" ~ '^[a-f0-9]{64}$' AND " +
            "\"ObjectKey\" = 'listings/photos/' || replace(\"Id\"::text, '-', '') AND isfinite(\"CreatedAt\") AND " +
            "(\"AvailableAt\" IS NULL OR (isfinite(\"AvailableAt\") AND \"AvailableAt\" >= \"CreatedAt\"))"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.Id, item.VehicleId, item.BranchId });
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(item => new { item.VehicleId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.ContentType).HasMaxLength(20).IsRequired();
        builder.Property(item => item.ObjectKey).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Sha256).HasMaxLength(64).IsRequired();
    }

    public void Configure(EntityTypeBuilder<ListingRevisionPhoto> builder)
    {
        builder.ToTable("ListingRevisionPhotos", table => table.HasCheckConstraint("CK_ListingRevisionPhotos_Position", "\"Position\" >= 0"));
        builder.HasKey(item => new { item.RevisionId, item.PhotoId });
        builder.HasIndex(item => new { item.RevisionId, item.Position }).IsUnique();
        builder.HasOne<ListingRevision>().WithMany().HasForeignKey(item => new { item.RevisionId, item.VehicleId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.VehicleId, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ListingPhoto>().WithMany().HasForeignKey(item => new { item.PhotoId, item.VehicleId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.VehicleId, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
    }
}
