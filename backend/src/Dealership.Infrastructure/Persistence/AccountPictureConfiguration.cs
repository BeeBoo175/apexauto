using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class AccountPictureConfiguration : IEntityTypeConfiguration<AccountPicture>
{
    public void Configure(EntityTypeBuilder<AccountPicture> builder)
    {
        builder.ToTable("AccountPictures", table =>
        {
            table.HasCheckConstraint("CK_AccountPictures_Content",
                "\"Bytes\" > 0 AND \"Sha256\" ~ '^[0-9a-f]{64}$' AND \"ContentType\" IN ('image/jpeg', 'image/png', 'image/webp')");
        });
        builder.HasKey(picture => picture.Id);
        // At most one current picture per account; replaced and removed rows remain as history.
        builder.HasIndex(picture => picture.UserId).IsUnique().HasFilter("\"RemovedAt\" IS NULL");
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(picture => picture.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(picture => picture.ObjectKey).HasMaxLength(200).IsRequired();
        builder.Property(picture => picture.ContentType).HasMaxLength(30).IsRequired();
        builder.Property(picture => picture.Sha256).HasMaxLength(64).IsRequired();
    }
}
