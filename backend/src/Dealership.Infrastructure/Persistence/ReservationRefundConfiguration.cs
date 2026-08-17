using Dealership.Domain.Customers;
using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class ReservationRefundConfiguration : IEntityTypeConfiguration<ReservationRefund>
{
    public void Configure(EntityTypeBuilder<ReservationRefund> builder)
    {
        builder.ToTable("ReservationRefunds", table => table.HasCheckConstraint("CK_ReservationRefunds_Shape",
            "\"AmountSen\" > 0 AND \"Attempt\" >= 1 AND isfinite(\"RequestedAt\") AND \"State\" IN ('Processing', 'Completed', 'Failed') AND " +
            "(\"State\" = 'Processing' AND \"SettledAt\" IS NULL OR \"State\" <> 'Processing' AND isfinite(\"SettledAt\") AND " +
            "\"SettledAt\" >= \"RequestedAt\")"));
        builder.HasKey(item => item.PaymentRequestId);
        builder.Property(item => item.PaymentRequestId).ValueGeneratedNever();
        builder.Property(item => item.State).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(item => item.BankCode).HasMaxLength(16).IsRequired();
        builder.Property(item => item.AccountLast4).HasMaxLength(4).IsRequired();
        builder.Property(item => item.AccountHolder).HasMaxLength(100).IsRequired();
        builder.Property(item => item.PayoutId).HasMaxLength(64);
        builder.HasOne<ReservationDepositPayment>().WithOne().HasForeignKey<ReservationRefund>(item => item.PaymentRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
        // The provider's callback names a payout, never a payment, so it finds the refund by payout.
        builder.HasIndex(item => item.PayoutId).IsUnique().HasFilter("\"PayoutId\" IS NOT NULL");
        builder.HasIndex(item => new { item.CustomerId, item.State });
    }
}
