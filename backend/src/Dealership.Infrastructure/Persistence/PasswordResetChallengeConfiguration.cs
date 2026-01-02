using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class PasswordResetChallengeConfiguration : IEntityTypeConfiguration<PasswordResetChallenge>
{
    public void Configure(EntityTypeBuilder<PasswordResetChallenge> builder)
    {
        builder.ToTable("PasswordResetChallenges", table =>
        {
            table.HasCheckConstraint("CK_PasswordResetChallenges_Lifetime", $"\"ExpiresAt\" = \"CreatedAt\" + interval '{PasswordRecoveryPolicy.LifetimeHours} hour'");
            table.HasCheckConstraint("CK_PasswordResetChallenges_Attempts", "\"DeliveryAttempts\" >= 0");
            table.HasCheckConstraint("CK_PasswordResetChallenges_Receipt", """
                ("ConsumedAt" IS NULL AND "CompletedSecurityStamp" IS NULL) OR
                ("ConsumedAt" IS NOT NULL AND "CompletedSecurityStamp" IS NOT NULL AND
                 length("CompletedSecurityStamp") > 0 AND "ConsumedAt" >= "CreatedAt" AND
                 "ConsumedAt" < "ExpiresAt" AND "InvalidatedAt" IS NULL)
                """);
            table.HasCheckConstraint("CK_PasswordResetChallenges_Identity", """
                "Id" <> '00000000-0000-0000-0000-000000000000'::uuid AND "RecipientEmail" ~ '[^[:space:]]'
                """);
        });
        builder.HasKey(challenge => challenge.Id);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(challenge => challenge.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(challenge => new { challenge.UserId, challenge.CreatedAt });
        builder.HasIndex(challenge => challenge.UserId).IsUnique()
            .HasFilter("\"InvalidatedAt\" IS NULL AND \"ConsumedAt\" IS NULL");
        builder.HasIndex(challenge => challenge.NextDeliveryAt);
        builder.Property(challenge => challenge.RecipientEmail).HasMaxLength(254).IsRequired();
        builder.Property(challenge => challenge.CompletedSecurityStamp).HasMaxLength(256);
    }
}
