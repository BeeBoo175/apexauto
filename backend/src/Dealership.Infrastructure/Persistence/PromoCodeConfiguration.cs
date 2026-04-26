using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        builder.ToTable("PromoCodes", table =>
        {
            table.HasCheckConstraint("CK_PromoCodes_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                "length(\"Code\") > 0 AND \"Discount\" > 0 AND \"Discount\" <= 100000 AND \"Version\" > 0 AND isfinite(\"ExpiresAtUtc\")");
            // Match .NET Trim, including non-ASCII surrounding whitespace.
            table.HasCheckConstraint("CK_PromoCodes_TrimmedCode",
                "\"Code\" = btrim(\"Code\", U&'\\0009\\000A\\000B\\000C\\000D\\0020\\0085\\00A0\\1680\\2000\\2001\\2002\\2003\\2004\\2005\\2006\\2007\\2008\\2009\\200A\\2028\\2029\\202F\\205F\\3000')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Code).HasMaxLength(PromoCode.CodeMaximumLength).UseCollation("C").IsRequired();
        builder.HasIndex(item => item.Code).IsUnique();
        builder.Property(item => item.Discount).HasPrecision(18, 2);
        builder.Property(item => item.Version).IsConcurrencyToken().HasDefaultValue(1L);
    }
}
