using Dealership.Domain.Sales;
using Dealership.Infrastructure.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class SaleDeliveryMailConfiguration : IEntityTypeConfiguration<SaleDeliveryMail>
{
    public void Configure(EntityTypeBuilder<SaleDeliveryMail> builder)
    {
        builder.ToTable("SaleDeliveryMailOutbox", table => table.HasCheckConstraint("CK_SaleDeliveryMailOutbox_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DeliveryAttempts\" >= 0 AND " +
            "(\"RecipientEmail\" IS NULL OR \"RecipientEmail\" ~ '[^[:space:]]') AND isfinite(\"NextDeliveryAt\") AND " +
            "((\"DeliveryClaimId\" IS NULL) = (\"DeliveryClaimExpiresAt\" IS NULL))"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.RecipientEmail).HasMaxLength(256);
        builder.HasIndex(item => item.SalesOrderId).IsUnique();
        builder.HasIndex(item => new { item.NextDeliveryAt, item.Id });
        builder.HasOne<SalesOrder>().WithOne().HasForeignKey<SaleDeliveryMail>(item => item.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SalesOrderEvent>().WithMany()
            .HasForeignKey(item => new { item.DeliveryOperationId, item.SalesOrderId })
            .HasPrincipalKey(item => new { item.Id, item.SalesOrderId }).OnDelete(DeleteBehavior.Restrict);
    }
}
