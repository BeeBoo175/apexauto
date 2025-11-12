using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class AccountSessionConfiguration : IEntityTypeConfiguration<AccountSession>
{
    public void Configure(EntityTypeBuilder<AccountSession> builder)
    {
        builder.ToTable("AccountSessions", table =>
        {
            table.HasCheckConstraint("CK_AccountSessions_Identity", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_AccountSessions_Stamp", "\"SecurityStamp\" ~ '[^[:space:]]'");
            table.HasCheckConstraint("CK_AccountSessions_Lifetime",
                "\"ExpiresAt\" > \"CreatedAt\" AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"CreatedAt\")");
        });
        builder.HasKey(session => session.Id);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(session => session.UserId);
        builder.Property(session => session.SecurityStamp).IsRequired();
    }
}
