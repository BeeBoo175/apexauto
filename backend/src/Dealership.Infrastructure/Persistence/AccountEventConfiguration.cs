using System.Text.Json;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class AccountEventConfiguration : IEntityTypeConfiguration<AccountEvent>
{
    private static readonly JsonSerializerOptions ChangeJson = new();

    public void Configure(EntityTypeBuilder<AccountEvent> builder)
    {
        builder.ToTable("AccountEvents", table =>
        {
            table.HasCheckConstraint("CK_AccountEvents_Shape",
                "isfinite(\"RecordedAt\") AND " +
                "\"Kind\" IN ('UsernameChanged', 'PictureRemoved', 'ProfileEdited', 'EmailChanged', 'BranchTransferred', " +
                "'Suspended', 'SuspensionLifted', 'Deactivated', 'Reactivated') AND " +
                "((\"Kind\" IN ('Suspended', 'Deactivated', 'BranchTransferred') AND \"Reason\" IS NOT NULL AND " +
                "\"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR " +
                "(\"Kind\" NOT IN ('Suspended', 'Deactivated', 'BranchTransferred') AND \"Reason\" IS NULL)) AND " +
                "(\"Changes\" IS NULL OR jsonb_typeof(\"Changes\") = 'array')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.TargetId, item.RecordedAt, item.Id });
        builder.HasIndex(item => item.ActorId);
        // Both identities are existing accounts; deactivation never deletes one, so history keeps its references.
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.TargetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Reason).HasMaxLength(AccountEvent.ReasonMaximumLength);
        builder.Property(item => item.Changes).HasColumnType("jsonb").HasConversion(
            changes => changes == null ? null : JsonSerializer.Serialize(changes, ChangeJson),
            json => json == null ? null : JsonSerializer.Deserialize<List<AccountChange>>(json, ChangeJson),
            new ValueComparer<IReadOnlyList<AccountChange>?>((left, right) => ReferenceEquals(left, right),
                value => value == null ? 0 : value.Count, value => value));
    }
}
