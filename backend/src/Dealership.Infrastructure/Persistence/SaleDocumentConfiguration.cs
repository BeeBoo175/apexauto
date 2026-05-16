using Dealership.Domain.Sales;
using Dealership.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class SaleDocumentConfiguration : IEntityTypeConfiguration<SaleDocument>,
    IEntityTypeConfiguration<ReceiptNumberCounter>, IEntityTypeConfiguration<HandoverNumberCounter>
{
    public void Configure(EntityTypeBuilder<SaleDocument> builder)
    {
        builder.ToTable("SaleDocuments", table => table.HasCheckConstraint("CK_SaleDocuments_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"IssuedAt\") AND " +
            "((\"Kind\" = 'PurchaseOrder' AND \"Number\" ~ '^VPO-[0-9]{4}-[0-9]{6}$') OR " +
            "(\"Kind\" = 'PaymentReceipt' AND \"Number\" ~ '^VRC-[0-9]{4}-[0-9]{6}$') OR " +
            "(\"Kind\" = 'HandoverNote' AND \"Number\" ~ '^VHN-[0-9]{4}-[0-9]{6}$')) AND " +
            "jsonb_typeof(\"ContentsJson\") = 'object' AND octet_length(\"ContentsJson\"::text) <= 65536"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Number).HasMaxLength(15).UseCollation("C");
        builder.Property(item => item.ContentsJson).HasColumnType("jsonb");
        builder.HasIndex(item => item.Number).IsUnique();
        builder.HasIndex(item => new { item.SalesOrderId, item.Kind }).IsUnique()
            .HasFilter("\"CorrectionOperationId\" IS NULL");
        builder.HasIndex(item => item.CorrectionOperationId).IsUnique()
            .HasFilter("\"CorrectionOperationId\" IS NOT NULL");
        builder.HasOne<SaleDocumentCorrection>().WithMany()
            .HasForeignKey(item => new { item.CorrectionOperationId, item.SalesOrderId })
            .HasPrincipalKey(item => new { item.Id, item.SalesOrderId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesOrder>().WithMany().HasForeignKey(item => item.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesOrderEvent>().WithMany()
            .HasForeignKey(item => new { item.IssuingOperationId, item.SalesOrderId })
            .HasPrincipalKey(item => new { item.Id, item.SalesOrderId }).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ReceiptNumberCounter> builder)
    {
        builder.ToTable("ReceiptNumberCounters", table => table.HasCheckConstraint("CK_ReceiptNumberCounters_Shape",
            "\"Year\" BETWEEN 2000 AND 9999 AND \"LastValue\" BETWEEN 0 AND 999999"));
        builder.HasKey(item => item.Year);
        builder.Property(item => item.Year).ValueGeneratedNever();
    }

    public void Configure(EntityTypeBuilder<HandoverNumberCounter> builder)
    {
        builder.ToTable("HandoverNumberCounters", table => table.HasCheckConstraint("CK_HandoverNumberCounters_Shape",
            "\"Year\" BETWEEN 2000 AND 9999 AND \"LastValue\" BETWEEN 0 AND 999999"));
        builder.HasKey(item => item.Year);
        builder.Property(item => item.Year).ValueGeneratedNever();
    }
}
