using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class RetentionSettingChangeConfiguration : IEntityTypeConfiguration<RetentionSettingChange>
{
    public void Configure(EntityTypeBuilder<RetentionSettingChange> builder)
    {
        builder.ToTable("RetentionSettingChanges", table => table.HasCheckConstraint("CK_RetentionSettingChanges_Shape",
            "\"Setting\" IN ('SecurityArtifactDays', 'MailDetailDays', 'InvitationDays', 'BusinessEvidenceYears') AND " +
            "\"PreviousValue\" > 0 AND \"NewValue\" > 0 AND \"PreviousValue\" <> \"NewValue\" AND " +
            "\"SettingsVersion\" > 1 AND isfinite(\"ChangedAtUtc\") AND " +
            "(NOT \"ShorteningConfirmed\" OR \"NewValue\" < \"PreviousValue\")"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Setting).HasMaxLength(32).IsRequired();
        builder.HasIndex(item => new { item.Setting, item.SettingsVersion }).IsUnique();
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.ActorId);
    }
}
