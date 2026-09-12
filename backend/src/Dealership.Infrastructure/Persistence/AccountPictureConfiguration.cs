using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class AccountPictureConfiguration : IEntityTypeConfiguration<AccountPicture>,
    IEntityTypeConfiguration<AccountPictureRemoval>
{
    public void Configure(EntityTypeBuilder<AccountPicture> builder)
    {
        builder.ToTable("AccountPictures", table =>
        {
            table.HasCheckConstraint("CK_AccountPictures_Content",
                "\"Bytes\" > 0 AND (\"Sha256\" IS NULL OR \"Sha256\" ~ '^[0-9a-f]{64}$') AND \"ContentType\" IN ('image/jpeg', 'image/png', 'image/webp')");
            table.HasCheckConstraint("CK_AccountPictures_Lifecycle",
                "isfinite(\"CreatedAt\") AND \"ExpiresAt\" > \"CreatedAt\" AND " +
                "(\"AvailableAt\" IS NULL OR (isfinite(\"AvailableAt\") AND \"AvailableAt\" >= \"CreatedAt\")) AND " +
                "(\"RemovedAt\" IS NULL OR (\"AvailableAt\" IS NOT NULL AND \"RemovedAt\" >= \"AvailableAt\"))");
        });
        builder.HasKey(picture => picture.Id);
        // At most one current picture per account; pending uploads, replaced and removed rows remain as history.
        builder.HasIndex(picture => picture.UserId).IsUnique()
            .HasFilter("\"RemovedAt\" IS NULL AND \"AvailableAt\" IS NOT NULL");
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(picture => picture.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(picture => picture.ObjectKey).HasMaxLength(200).IsRequired();
        builder.Property(picture => picture.ContentType).HasMaxLength(30).IsRequired();
        builder.Property(picture => picture.Sha256).HasMaxLength(64);
    }

    public void Configure(EntityTypeBuilder<AccountPictureRemoval> builder)
    {
        builder.ToTable("AccountPictureRemovals", table => table.HasCheckConstraint("CK_AccountPictureRemovals_Shape",
            "\"Reason\" IN ('Removed', 'Replaced', 'UnconfirmedExpired') AND isfinite(\"EligibleAt\") AND " +
            "isfinite(\"RequestedAt\") AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND " +
            "isfinite(\"NextAttemptAt\") AND (\"DeletedAt\" IS NULL OR (isfinite(\"DeletedAt\") AND \"DeletedAt\" >= \"RequestedAt\")) AND " +
            "((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL)) AND (\"ClaimId\" IS NULL OR " +
            "(\"ClaimId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"ClaimExpiresAt\")))"));
        builder.HasKey(item => item.PictureId);
        builder.Property(item => item.PictureId).ValueGeneratedNever();
        builder.Property(item => item.Reason).HasConversion<string>().HasMaxLength(32);
        builder.HasOne<AccountPicture>().WithOne().HasForeignKey<AccountPictureRemoval>(item => item.PictureId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.NextAttemptAt, item.PictureId });
    }
}
