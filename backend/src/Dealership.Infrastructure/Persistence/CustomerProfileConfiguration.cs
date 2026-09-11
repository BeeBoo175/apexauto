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
            // Biconditionals on ErasedAt: an unerased profile must be complete, an erased one must hold none of it.
            table.HasCheckConstraint("CK_CustomerProfiles_Nric",
                "(\"ErasedAt\" IS NULL AND \"Nric\" IS NOT NULL AND \"Nric\" ~ '^[0-9]{12}$') OR (\"ErasedAt\" IS NOT NULL AND \"Nric\" IS NULL)");
            table.HasCheckConstraint("CK_CustomerProfiles_RequiredText",
                "(\"ErasedAt\" IS NULL AND \"LegalName\" IS NOT NULL AND \"Mobile\" IS NOT NULL AND \"ResidentialAddress\" IS NOT NULL AND \"LegalName\" ~ '[^[:space:]]' AND \"Mobile\" ~ '[^[:space:]]' AND \"ResidentialAddress\" ~ '[^[:space:]]') OR " +
                "(\"ErasedAt\" IS NOT NULL AND \"LegalName\" IS NULL AND \"Mobile\" IS NULL AND \"ResidentialAddress\" IS NULL)");
            table.HasCheckConstraint("CK_CustomerProfiles_RegistrationKey", "\"RegistrationKey\" <> '00000000-0000-0000-0000-000000000000'::uuid");
        });
        builder.HasKey(profile => profile.UserId);
        // A profile shares its user's key; RESTRICT prevents deleting the referenced account.
        builder.HasOne<AccountUser>().WithOne().HasForeignKey<CustomerProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(profile => profile.Nric).IsUnique();
        builder.HasIndex(profile => profile.RegistrationKey).IsUnique();
        // Staff search names and mobile numbers by any part of them (ILIKE '%text%'), which only a trigram index serves.
        builder.HasIndex(profile => profile.LegalName).HasMethod("gin").HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_CustomerProfiles_LegalName_Trigram");
        builder.HasIndex(profile => profile.Mobile).HasMethod("gin").HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_CustomerProfiles_Mobile_Trigram");
        builder.Property(profile => profile.LegalName).HasMaxLength(CustomerProfile.LegalNameMaximumLength);
        builder.Property(profile => profile.Nric).HasMaxLength(CustomerProfile.NricLength);
        builder.Property(profile => profile.Mobile).HasMaxLength(CustomerProfile.MobileMaximumLength);
        builder.Property(profile => profile.ResidentialAddress).HasMaxLength(CustomerProfile.AddressMaximumLength);
    }
}
