using Dealership.Domain.Branches;
using Dealership.Domain.Intake;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>,
    IEntityTypeConfiguration<SalesOrderEvent>, IEntityTypeConfiguration<DealNumberCounter>
{
    public const string OpenStatuses = "'Draft', 'PendingManagerApproval', 'Approved'";

    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders", table =>
        {
            table.HasCheckConstraint("CK_SalesOrders_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Version\" > 0 AND " +
                "\"Status\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled') AND " +
                "isfinite(\"CreatedAtUtc\") AND isfinite(\"UpdatedAtUtc\") AND \"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND " +
                "((\"OrderNumber\" IS NULL AND \"SubmittedAtUtc\" IS NULL) OR " +
                "(\"OrderNumber\" ~ '^VPO-[0-9]{4}-[0-9]{6}$' AND \"SubmittedAtUtc\" IS NOT NULL))");
            table.HasCheckConstraint("CK_SalesOrders_Price",
                "\"BasePrice\" > 0 AND \"RoadTax\" >= 0 AND \"ProcessingFee\" >= 0 AND \"SstRate\" BETWEEN 0 AND 1 AND " +
                "\"PromoDiscount\" >= 0 AND \"NegotiatedDiscount\" >= 0 AND \"WarrantyPrice\" >= 0 AND " +
                "\"DepositCredit\" >= 0 AND \"TradeInAllowance\" >= 0 AND " +
                "\"PromoDiscount\" + \"NegotiatedDiscount\" <= \"BasePrice\" AND " +
                "\"ServiceTax\" = round(\"ProcessingFee\" * \"SstRate\", 2) AND \"BalancePayable\" >= 0 AND " +
                "\"BalancePayable\" = \"BasePrice\" - \"PromoDiscount\" - \"NegotiatedDiscount\" + \"ProcessingFee\" + " +
                "\"RoadTax\" + \"ServiceTax\" + \"WarrantyPrice\" - \"DepositCredit\" - \"TradeInAllowance\"");
            table.HasCheckConstraint("CK_SalesOrders_Terms",
                "((\"PromoId\" IS NULL AND \"PromoCode\" IS NULL AND \"PromoDiscount\" = 0) OR " +
                "(\"PromoId\" IS NOT NULL AND \"PromoCode\" IS NOT NULL)) AND " +
                "((\"WarrantyPackageId\" IS NULL AND \"WarrantyName\" IS NULL AND \"WarrantyPrice\" = 0) OR " +
                "(\"WarrantyPackageId\" IS NOT NULL AND \"WarrantyName\" IS NOT NULL)) AND " +
                "((\"TradeInAppointmentId\" IS NULL AND \"TradeInAllowance\" = 0) OR \"TradeInAppointmentId\" IS NOT NULL) AND " +
                "(\"ReservationId\" IS NOT NULL OR \"DepositCredit\" = 0)");
            table.HasCheckConstraint("CK_SalesOrders_Approval",
                "(\"Status\" = 'Draft' AND \"DecidedById\" IS NULL AND \"DiscountLimitAtSubmit\" IS NULL) OR " +
                "(\"Status\" = 'PendingManagerApproval' AND \"DecidedById\" IS NULL AND " +
                "\"DiscountLimitAtSubmit\" IS NOT NULL AND \"NegotiatedDiscount\" > \"DiscountLimitAtSubmit\") OR " +
                "(\"Status\" = 'Approved' AND \"DiscountLimitAtSubmit\" IS NOT NULL AND " +
                "(\"DecidedById\" IS NOT NULL OR \"NegotiatedDiscount\" <= \"DiscountLimitAtSubmit\") AND " +
                "\"DecidedById\" IS DISTINCT FROM \"SalesAgentId\") OR \"Status\" = 'Cancelled'");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.OrderNumber).HasMaxLength(20).UseCollation("C");
        builder.HasIndex(item => item.OrderNumber).IsUnique().HasFilter("\"OrderNumber\" IS NOT NULL");
        // One open deal per vehicle; cancelled deals no longer hold it.
        builder.HasIndex(item => item.VehicleId).IsUnique().HasFilter($"\"Status\" IN ({OpenStatuses})");
        builder.HasIndex(item => new { item.BranchId, item.CreatedAtUtc, item.Id });
        builder.HasIndex(item => new { item.SalesAgentId, item.CreatedAtUtc, item.Id });
        builder.HasIndex(item => new { item.CustomerId, item.CreatedAtUtc, item.Id });
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.PromoCode).HasMaxLength(PromoCode.CodeMaximumLength).UseCollation("C");
        builder.Property(item => item.WarrantyName).HasMaxLength(WarrantyPackage.NameMaximumLength);
        builder.Property(item => item.SstRate).HasPrecision(29, 28);
        foreach (var money in new[]
        {
            nameof(SalesOrder.BasePrice), nameof(SalesOrder.RoadTax), nameof(SalesOrder.ProcessingFee),
            nameof(SalesOrder.ServiceTax), nameof(SalesOrder.PromoDiscount), nameof(SalesOrder.NegotiatedDiscount),
            nameof(SalesOrder.WarrantyPrice), nameof(SalesOrder.DepositCredit), nameof(SalesOrder.TradeInAllowance),
            nameof(SalesOrder.BalancePayable), nameof(SalesOrder.DiscountLimitAtSubmit)
        })
        {
            builder.Property(money).HasPrecision(18, 2);
        }

        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(item => item.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(item => item.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.SalesAgentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.DecidedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Reservation>().WithMany().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PromoCode>().WithMany().HasForeignKey(item => item.PromoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarrantyPackage>().WithMany().HasForeignKey(item => item.WarrantyPackageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IntakeAppointment>().WithMany().HasForeignKey(item => item.TradeInAppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<SalesOrderEvent> builder)
    {
        builder.ToTable("SalesOrderEvents", table =>
        {
            table.HasCheckConstraint("CK_SalesOrderEvents_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAtUtc\") AND " +
                "\"Sequence\" > 0 AND \"Kind\" IN ('Created', 'Revised', 'Submitted', 'Approved', 'Rejected', 'Cancelled') AND " +
                "\"ToStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled') AND " +
                "(\"FromStatus\" IS NULL OR \"FromStatus\" IN ('Draft', 'PendingManagerApproval', 'Approved', 'Cancelled')) AND " +
                "((\"Kind\" IN ('Rejected', 'Cancelled') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND " +
                "length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR " +
                "(\"Kind\" NOT IN ('Rejected', 'Cancelled') AND \"Reason\" IS NULL))");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.SalesOrderId, item.Sequence }).IsUnique();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.FromStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.ToStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Reason).HasMaxLength(SalesOrderEvent.ReasonMaximumLength);
        builder.HasOne<SalesOrder>().WithMany().HasForeignKey(item => item.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<DealNumberCounter> builder)
    {
        builder.ToTable("DealNumberCounters", table => table.HasCheckConstraint("CK_DealNumberCounters_Shape",
            "\"Year\" BETWEEN 2000 AND 9999 AND \"LastValue\" BETWEEN 0 AND 999999"));
        builder.HasKey(item => item.Year);
        builder.Property(item => item.Year).ValueGeneratedNever();
    }
}
