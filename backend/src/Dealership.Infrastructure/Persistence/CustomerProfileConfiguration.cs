using Dealership.Domain.Customers;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.ToTable("CustomerProfiles", table =>
        {
            table.HasCheckConstraint("CK_CustomerProfiles_Nric", "\"Nric\" ~ '^[0-9]{12}$'");
            table.HasCheckConstraint("CK_CustomerProfiles_RequiredText",
                "\"LegalName\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]' AND \"ResidentialAddress\" ~ '[^[:space:]]'");
            table.HasCheckConstraint("CK_CustomerProfiles_RegistrationKey", "\"RegistrationKey\" <> '00000000-0000-0000-0000-000000000000'::uuid");
        });
        builder.HasKey(profile => profile.UserId);
        // A profile shares its user's key; RESTRICT prevents deleting the referenced account.
        builder.HasOne<AccountUser>().WithOne().HasForeignKey<CustomerProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(profile => profile.Nric).IsUnique();
        builder.HasIndex(profile => profile.RegistrationKey).IsUnique();
        builder.Property(profile => profile.LegalName).HasMaxLength(CustomerProfile.LegalNameMaximumLength).IsRequired();
        builder.Property(profile => profile.Nric).HasMaxLength(CustomerProfile.NricLength).IsRequired();
        builder.Property(profile => profile.Mobile).HasMaxLength(CustomerProfile.MobileMaximumLength).IsRequired();
        builder.Property(profile => profile.ResidentialAddress).HasMaxLength(CustomerProfile.AddressMaximumLength).IsRequired();
    }
}
