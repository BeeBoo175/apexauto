using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class AccountUserConfiguration : IEntityTypeConfiguration<AccountUser>
{
    public void Configure(EntityTypeBuilder<AccountUser> builder)
    {
        var branchRoles = $"'{AccountRoles.SalesAgent}', '{AccountRoles.WorkshopTechnician}', '{AccountRoles.BranchManager}'";
        var unassignedRoles = $"'{AccountRoles.Customer}', '{AccountRoles.GeneralManager}', '{AccountRoles.Administrator}'";
        var allowedRoles = $"'{AccountRoles.Customer}', {branchRoles}, '{AccountRoles.GeneralManager}', '{AccountRoles.Administrator}'";
        builder.ToTable("AspNetUsers", table =>
        {
            table.HasCheckConstraint("CK_Users_EmailNormalization",
                // Explicit Unicode White_Space characters match .NET Trim rather than PostgreSQL's space-only default.
                "length(\"Email\") > 0 AND \"Email\" = btrim(\"Email\", U&'\\0009\\000A\\000B\\000C\\000D\\0020\\0085\\00A0\\1680\\2000\\2001\\2002\\2003\\2004\\2005\\2006\\2007\\2008\\2009\\200A\\2028\\2029\\202F\\205F\\3000') AND \"NormalizedEmail\" = upper(\"Email\")");
            table.HasCheckConstraint("CK_Users_AccountUsername",
                $"\"AccountUsername\" ~ '^[A-Za-z0-9._]{{{UsernamePolicy.MinimumLength},{UsernamePolicy.MaximumLength}}}$'");
            table.HasCheckConstraint("CK_Users_SuspendedInactive", "\"SuspendedAt\" IS NULL OR NOT \"IsActive\"");
            table.HasCheckConstraint("CK_Users_RoleBranch",
                $"\"Role\" IN ({allowedRoles}) AND " +
                $"((\"Role\" IN ({branchRoles}) AND \"BranchId\" IS NOT NULL) OR " +
                $"(\"Role\" IN ({unassignedRoles}) AND \"BranchId\" IS NULL))");
        });
        // A required branch-scoped actor cannot survive deletion of its assigned branch.
        builder.HasOne<Dealership.Domain.Branches.Branch>().WithMany().HasForeignKey(user => user.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(user => user.BranchId);
        // The customer picker and lists search email by any part of it, which only a trigram index serves.
        builder.HasIndex(user => user.Email).HasMethod("gin").HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_AspNetUsers_Email_Trigram");
        builder.Property(user => user.Email).HasMaxLength(AccountCredentialPolicy.EmailMaximumLength).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(AccountCredentialPolicy.EmailMaximumLength).IsRequired();
        builder.Property(user => user.AccountUsername).HasMaxLength(UsernamePolicy.MaximumLength).IsRequired();
        // Uniqueness ignores case through a stored uppercase key, as email does with NormalizedEmail.
        builder.Property<string>("NormalizedAccountUsername").HasMaxLength(UsernamePolicy.MaximumLength)
            .HasComputedColumnSql("upper(\"AccountUsername\")", stored: true);
        builder.HasIndex("NormalizedAccountUsername").HasDatabaseName("AccountUsernameIndex").IsUnique();
        builder.Property(user => user.Role).HasMaxLength(32).HasDefaultValue(AccountRoles.Customer).IsRequired();
        builder.Property(user => user.IsActive).HasDefaultValue(true);
        builder.HasIndex(user => user.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
    }
}
