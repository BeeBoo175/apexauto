using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class EmailVerificationChallengeConfiguration : IEntityTypeConfiguration<EmailVerificationChallenge>
{
    public void Configure(EntityTypeBuilder<EmailVerificationChallenge> builder)
    {
        builder.ToTable("EmailVerificationChallenges", table =>
        {
            table.HasCheckConstraint("CK_EmailVerificationChallenges_Lifetime", "\"ExpiresAt\" > \"CreatedAt\"");
            table.HasCheckConstraint("CK_EmailVerificationChallenges_Attempts", "\"DeliveryAttempts\" >= 0");
        });
        builder.HasKey(challenge => challenge.Id);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(challenge => challenge.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(challenge => new { challenge.UserId, challenge.CreatedAt });
        builder.HasIndex(challenge => challenge.UserId).IsUnique()
            .HasFilter("\"InvalidatedAt\" IS NULL AND \"ConsumedAt\" IS NULL");
        builder.HasIndex(challenge => challenge.NextDeliveryAt);
        builder.HasIndex(challenge => challenge.ResendKey).IsUnique()
            .HasFilter("\"ResendKey\" IS NOT NULL");
        builder.Property(challenge => challenge.RecipientEmail).HasMaxLength(254).IsRequired();
    }
}
