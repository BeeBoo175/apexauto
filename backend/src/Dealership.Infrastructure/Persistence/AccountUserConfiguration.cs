using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class AccountUserConfiguration : IEntityTypeConfiguration<AccountUser>
{
    public void Configure(EntityTypeBuilder<AccountUser> builder)
    {
        builder.ToTable("AspNetUsers", table =>
        {
            table.HasCheckConstraint("CK_Users_EmailNormalization",
                // Explicit Unicode White_Space characters match .NET Trim rather than PostgreSQL's space-only default.
                "length(\"Email\") > 0 AND \"Email\" = btrim(\"Email\", U&'\\0009\\000A\\000B\\000C\\000D\\0020\\0085\\00A0\\1680\\2000\\2001\\2002\\2003\\2004\\2005\\2006\\2007\\2008\\2009\\200A\\2028\\2029\\202F\\205F\\3000') AND \"NormalizedEmail\" = upper(\"Email\")");
        });
        builder.Property(user => user.Email).HasMaxLength(RegistrationService.EmailMaximumLength).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(RegistrationService.EmailMaximumLength).IsRequired();
        builder.HasIndex(user => user.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
    }
}
