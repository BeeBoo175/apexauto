using Dealership.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class ReservationDepositPaymentConfiguration : IEntityTypeConfiguration<ReservationDepositPayment>
{
    public void Configure(EntityTypeBuilder<ReservationDepositPayment> builder)
    {
        builder.ToTable("ReservationDepositPayments", table => table.HasCheckConstraint("CK_ReservationDepositPayments_Shape",
            "\"AmountSen\" > 0 AND isfinite(\"CreatedAt\") AND (\"State\" = 'Pending' AND \"PaidAt\" IS NULL AND " +
            "\"PaidAmountSen\" IS NULL OR \"State\" IN ('Paid', 'PaidUnmatched') AND isfinite(\"PaidAt\") AND " +
            "\"PaidAt\" >= \"CreatedAt\" AND \"PaidAmountSen\" >= 0)"));
        builder.HasKey(item => item.RequestId);
        builder.Property(item => item.RequestId).ValueGeneratedNever();
        builder.Property(item => item.BillId).HasMaxLength(64).IsRequired();
        builder.Property(item => item.BillUrl).HasMaxLength(512).IsRequired();
        builder.Property(item => item.State).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasOne<ReservationDepositRequest>().WithOne().HasForeignKey<ReservationDepositPayment>(item => item.RequestId)
            .OnDelete(DeleteBehavior.Restrict);
        // The gateway names a bill, never a request, so its callback finds the payment by bill.
        builder.HasIndex(item => item.BillId).IsUnique();
    }
}
