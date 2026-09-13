using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class RetentionSettingsConfiguration : IEntityTypeConfiguration<RetentionSettings>
{
    public void Configure(EntityTypeBuilder<RetentionSettings> builder)
    {
        builder.ToTable("RetentionSettings", table => table.HasCheckConstraint("CK_RetentionSettings_Shape",
            "\"Id\" = 1 AND \"SecurityArtifactDays\" BETWEEN 7 AND 365 AND \"MailDetailDays\" BETWEEN 7 AND 365 AND " +
            "\"InvitationDays\" BETWEEN 7 AND 365 AND \"BusinessEvidenceYears\" BETWEEN 1 AND 10 AND " +
            "\"Version\" > 0 AND isfinite(\"UpdatedAtUtc\") AND " +
            "\"UpdatedById\" <> '00000000-0000-0000-0000-000000000000'::uuid"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.UpdatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
