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
            table.HasCheckConstraint("CK_Users_RoleBranch",
                $"\"Role\" IN ({allowedRoles}) AND " +
                $"((\"Role\" IN ({branchRoles}) AND \"BranchId\" IS NOT NULL) OR " +
                $"(\"Role\" IN ({unassignedRoles}) AND \"BranchId\" IS NULL))");
        });
        // A required branch-scoped actor cannot survive deletion of its assigned branch.
        builder.HasOne<Dealership.Domain.Branches.Branch>().WithMany().HasForeignKey(user => user.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(user => user.BranchId);
        builder.Property(user => user.Email).HasMaxLength(AccountCredentialPolicy.EmailMaximumLength).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(AccountCredentialPolicy.EmailMaximumLength).IsRequired();
        builder.Property(user => user.Role).HasMaxLength(32).HasDefaultValue(AccountRoles.Customer).IsRequired();
        builder.Property(user => user.IsActive).HasDefaultValue(true);
        builder.HasIndex(user => user.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
    }
}
