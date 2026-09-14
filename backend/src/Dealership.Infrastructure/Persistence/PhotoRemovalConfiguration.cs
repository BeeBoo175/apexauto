using Dealership.Domain.Intake;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class PhotoRemovalConfiguration : IEntityTypeConfiguration<PhotoRemoval>
{
    public void Configure(EntityTypeBuilder<PhotoRemoval> builder)
    {
        builder.ToTable("PhotoRemovals", table =>
        {
            table.HasCheckConstraint("CK_PhotoRemovals_Shape",
                "\"Reason\" IN ('UnsubmittedExpired', 'CancelledRetentionExpired', 'OwnerErased', 'RetentionExpired') AND \"RequestedAt\" >= \"EligibleAt\" AND \"Attempts\" >= 0 AND (\"DeletedAt\" IS NULL OR \"DeletedAt\" >= \"RequestedAt\") AND ((\"ClaimId\" IS NULL) = (\"ClaimExpiresAt\" IS NULL))");
        });
        builder.HasKey(item => item.PhotoId);
        builder.Property(item => item.PhotoId).ValueGeneratedNever();
        builder.Property(item => item.Reason).HasConversion<string>().HasMaxLength(32);
        builder.HasOne<IntakePhoto>().WithOne().HasForeignKey<PhotoRemoval>(item => item.PhotoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.NextAttemptAt, item.PhotoId });
    }
}
