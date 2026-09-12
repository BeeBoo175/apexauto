using Dealership.Domain.Staff;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class StaffProfileConfiguration : IEntityTypeConfiguration<StaffProfile>
{
    public void Configure(EntityTypeBuilder<StaffProfile> builder)
    {
        builder.ToTable("StaffProfiles", table =>
        {
            table.HasCheckConstraint("CK_StaffProfiles_RequiredText",
                "(\"ErasedAt\" IS NULL AND \"Name\" IS NOT NULL AND \"Mobile\" IS NOT NULL AND \"Name\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]') OR " +
                "(\"ErasedAt\" IS NOT NULL AND \"Name\" IS NULL AND \"Mobile\" IS NULL)");
        });
        builder.HasKey(profile => profile.UserId);
        // A profile shares its user's key; RESTRICT prevents deleting the referenced account.
        builder.HasOne<AccountUser>().WithOne().HasForeignKey<StaffProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(profile => profile.Name).HasMaxLength(StaffProfile.NameMaximumLength);
        builder.Property(profile => profile.Mobile).HasMaxLength(StaffProfile.MobileMaximumLength);
    }
}
