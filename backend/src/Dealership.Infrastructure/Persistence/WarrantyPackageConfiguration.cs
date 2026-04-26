using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class WarrantyPackageConfiguration : IEntityTypeConfiguration<WarrantyPackage>
{
    public void Configure(EntityTypeBuilder<WarrantyPackage> builder)
    {
        builder.ToTable("WarrantyPackages", table =>
        {
            table.HasCheckConstraint("CK_WarrantyPackages_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND length(btrim(\"Name\")) > 0 AND " +
                "\"Name\" = btrim(\"Name\") AND \"DurationMonths\" BETWEEN 1 AND 120 AND " +
                "\"MileageCapKm\" BETWEEN 1 AND 500000 AND \"Price\" BETWEEN 0 AND 20000 AND " +
                "\"MinimumGrade\" IN ('A', 'B', 'C') AND \"Version\" > 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Name).HasMaxLength(WarrantyPackage.NameMaximumLength).UseCollation("C").IsRequired();
        builder.HasIndex(item => item.Name).IsUnique();
        builder.Property(item => item.Price).HasPrecision(18, 2);
        builder.Property(item => item.MinimumGrade).HasConversion<string>().HasMaxLength(1);
        builder.Property(item => item.Version).IsConcurrencyToken();
    }
}
