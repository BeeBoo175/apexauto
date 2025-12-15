using Dealership.Domain.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches", table =>
        {
            table.HasCheckConstraint("CK_Branches_Identity", "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_Branches_RequiredText",
                "\"Name\" ~ '[^[:space:]]' AND \"Address\" ~ '[^[:space:]]' AND \"City\" ~ '[^[:space:]]' AND \"State\" ~ '[^[:space:]]' AND \"Phone\" ~ '[^[:space:]]'");
        });
        builder.HasKey(branch => branch.Id);
        builder.HasIndex(branch => branch.Name).IsUnique();
        builder.Property(branch => branch.Name).HasMaxLength(Branch.NameMaximumLength).IsRequired();
        builder.Property(branch => branch.Address).HasMaxLength(Branch.AddressMaximumLength).IsRequired();
        builder.Property(branch => branch.City).HasMaxLength(Branch.CityMaximumLength).IsRequired();
        builder.Property(branch => branch.State).HasMaxLength(Branch.StateMaximumLength).IsRequired();
        builder.Property(branch => branch.Phone).HasMaxLength(Branch.PhoneMaximumLength).IsRequired();
    }
}
