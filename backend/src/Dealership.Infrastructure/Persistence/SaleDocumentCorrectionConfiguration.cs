using Dealership.Domain.Sales;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class SaleDocumentCorrectionConfiguration : IEntityTypeConfiguration<SaleDocumentCorrection>
{
    public void Configure(EntityTypeBuilder<SaleDocumentCorrection> builder)
    {
        // The buyer spelling and reason are all present, or all removed together with the marker after retention.
        builder.ToTable("SaleDocumentCorrections", table => table.HasCheckConstraint("CK_SaleDocumentCorrections_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ExpectedVersion\" > 0 AND " +
            "isfinite(\"RecordedAt\") AND ((\"MinimizedAt\" IS NULL AND \"BuyerName\" IS NOT NULL AND " +
            "\"BuyerAddress\" IS NOT NULL AND \"Reason\" IS NOT NULL AND length(\"BuyerName\") BETWEEN 1 AND 200 AND " +
            "length(\"BuyerAddress\") BETWEEN 1 AND 1000 AND length(\"Reason\") BETWEEN 1 AND 1000 AND " +
            "\"BuyerName\" = btrim(\"BuyerName\") AND \"BuyerName\" ~ '[^[:space:]]' AND \"BuyerName\" !~ '[[:cntrl:]]' AND " +
            "\"BuyerAddress\" = btrim(\"BuyerAddress\") AND \"BuyerAddress\" ~ '[^[:space:]]' AND \"BuyerAddress\" !~ '[[:cntrl:]]' AND " +
            "\"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]') OR " +
            "(\"MinimizedAt\" IS NOT NULL AND \"BuyerName\" IS NULL AND \"BuyerAddress\" IS NULL AND \"Reason\" IS NULL))"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.BuyerName).HasMaxLength(200);
        builder.Property(item => item.BuyerAddress).HasMaxLength(1000);
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.HasIndex(item => item.OriginalDocumentId).IsUnique();
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesOrder>().WithMany().HasForeignKey(item => item.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SaleDocument>().WithMany()
            .HasForeignKey(item => new { item.OriginalDocumentId, item.SalesOrderId })
            .HasPrincipalKey(item => new { item.Id, item.SalesOrderId }).OnDelete(DeleteBehavior.Restrict);
    }
}
