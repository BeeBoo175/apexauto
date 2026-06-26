using Dealership.Application.Accounts;
using Dealership.Domain.Staff;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class StaffInvitationConfiguration : IEntityTypeConfiguration<StaffInvitation>
{
    public void Configure(EntityTypeBuilder<StaffInvitation> builder)
    {
        builder.ToTable("StaffInvitations", table =>
        {
            table.HasCheckConstraint("CK_StaffInvitations_Role",
                "\"Role\" IN ('SalesAgent', 'WorkshopTechnician', 'BranchManager', 'GeneralManager', 'Administrator') AND " +
                "((\"Role\" IN ('SalesAgent', 'WorkshopTechnician', 'BranchManager') AND \"BranchId\" IS NOT NULL) OR " +
                "(\"Role\" IN ('GeneralManager', 'Administrator') AND \"BranchId\" IS NULL))");
            table.HasCheckConstraint("CK_StaffInvitations_Profile",
                "\"Name\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]'");
            table.HasCheckConstraint("CK_StaffInvitations_Lifetime",
                "\"ExpiresAt\" > \"CreatedAt\" AND \"DeliveryAttempts\" >= 0");
            // An invitation is activated or revoked, never both.
            table.HasCheckConstraint("CK_StaffInvitations_Outcome",
                "(\"ActivatedAt\" IS NULL OR \"RevokedAt\" IS NULL) AND " +
                "(\"ActivatedAt\" IS NULL) = (\"ActivatedUserId\" IS NULL) AND " +
                "(\"RevokedAt\" IS NULL) = (\"RevokedById\" IS NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Email).HasMaxLength(AccountCredentialPolicy.EmailMaximumLength).IsRequired();
        // One pending invitation per email ignoring case; revoking or activating releases it.
        builder.Property<string>("NormalizedEmail").HasMaxLength(AccountCredentialPolicy.EmailMaximumLength)
            .HasComputedColumnSql("upper(\"Email\")", stored: true);
        builder.HasIndex("NormalizedEmail").HasDatabaseName("StaffInvitationPendingEmailIndex").IsUnique()
            .HasFilter("\"ActivatedAt\" IS NULL AND \"RevokedAt\" IS NULL");
        builder.Property(item => item.Role).HasMaxLength(32).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(StaffProfile.NameMaximumLength).IsRequired();
        builder.Property(item => item.Mobile).HasMaxLength(StaffProfile.MobileMaximumLength).IsRequired();
        builder.HasIndex(item => item.NextDeliveryAt);
        builder.HasIndex(item => item.BranchId);
        builder.HasIndex(item => item.InvitedById);
        // Branch and actors are existing records; the invited person has no account until activation.
        builder.HasOne<Dealership.Domain.Branches.Branch>().WithMany().HasForeignKey(item => item.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.InvitedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.RevokedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActivatedUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.ActivatedUserId).IsUnique().HasFilter("\"ActivatedUserId\" IS NOT NULL");
    }
}
