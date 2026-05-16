using System.Text.Json;
using Dealership.Domain.Branches;
using Dealership.Domain.Intake;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>,
    IEntityTypeConfiguration<SalesOrderEvent>, IEntityTypeConfiguration<DealNumberCounter>
{
    public const string HandledStatuses = "'Handling', 'PendingApproval', 'Approved'";

    private const string Statuses = "'Open', 'Handling', 'PendingApproval', 'Approved', 'Expired', 'Cancelled', 'Settled'";

    // Snapshots keep the sale's own member names, which are also its column names, so SQL can record the same shape.
    private static readonly JsonSerializerOptions SnapshotJson = new();

    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders", table =>
        {
            table.HasCheckConstraint("CK_SalesOrders_Shape",
                $"\"Version\" > 0 AND \"Status\" IN ({Statuses}) AND isfinite(\"CreatedAt\") AND " +
                "isfinite(\"UpdatedAt\") AND \"UpdatedAt\" >= \"CreatedAt\" AND " +
                "((\"OrderNumber\" IS NULL AND \"SubmittedAt\" IS NULL) OR " +
                "(\"OrderNumber\" ~ '^VPO-[0-9]{4}-[0-9]{6}$' AND \"SubmittedAt\" IS NOT NULL))");
            // An open sale runs against its deadline; a handled one has an agent and paused time; a closed one is dated.
            table.HasCheckConstraint("CK_SalesOrders_Handling",
                "(\"Status\" = 'Open' AND \"DueAt\" IS NOT NULL AND \"AgentId\" IS NULL AND \"ClaimedAt\" IS NULL AND " +
                "\"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NULL) OR " +
                $"(\"Status\" IN ({HandledStatuses}) AND \"DueAt\" IS NULL AND \"AgentId\" IS NOT NULL AND " +
                "\"ClaimedAt\" IS NOT NULL AND \"PausedSeconds\" BETWEEN 1 AND 172800 AND \"ClosedAt\" IS NULL) OR " +
                "(\"Status\" IN ('Expired', 'Cancelled') AND \"AgentId\" IS NULL AND \"ClaimedAt\" IS NULL AND " +
                "\"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NOT NULL) OR " +
                "(\"Status\" = 'Settled' AND \"DueAt\" IS NULL AND \"AgentId\" IS NOT NULL AND " +
                "\"ClaimedAt\" IS NOT NULL AND \"PausedSeconds\" IS NULL AND \"ClosedAt\" IS NULL)");
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
                "((\"TradeInAppointmentId\" IS NULL AND \"TradeInLabel\" IS NULL AND \"TradeInAllowance\" = 0) OR " +
                "(\"TradeInAppointmentId\" IS NOT NULL AND \"TradeInLabel\" IS NOT NULL AND \"TradeInAllowance\" > 0))");
            table.HasCheckConstraint("CK_SalesOrders_Approval",
                "(\"Status\" = 'PendingApproval' AND \"DiscountLimitAtSubmit\" IS NOT NULL AND \"DecidedById\" IS NULL AND " +
                "\"NegotiatedDiscount\" > \"DiscountLimitAtSubmit\") OR " +
                "(\"Status\" IN ('Approved', 'Settled') AND \"DiscountLimitAtSubmit\" IS NOT NULL AND \"DecidedAt\" IS NOT NULL AND " +
                "(\"DecidedById\" IS NOT NULL OR \"NegotiatedDiscount\" <= \"DiscountLimitAtSubmit\") AND " +
                "\"DecidedById\" IS DISTINCT FROM \"AgentId\") OR " +
                "(\"Status\" IN ('Open', 'Handling') AND \"DiscountLimitAtSubmit\" IS NULL AND \"DecidedById\" IS NULL) OR " +
                "\"Status\" IN ('Expired', 'Cancelled')");
            table.HasCheckConstraint("CK_SalesOrders_Settlement",
                "(\"Status\" = 'Settled' AND \"PaymentEvidence\" IS NOT NULL AND \"SettledAt\" IS NOT NULL AND " +
                "isfinite(\"SettledAt\") AND \"SettledAt\" >= \"DecidedAt\" AND \"SettledAt\" <= \"UpdatedAt\" AND " +
                "(\"TradeInAppointmentId\" IS NULL) = (\"AcquiredVehicleId\" IS NULL) AND " +
                "\"AcquiredVehicleId\" IS DISTINCT FROM \"VehicleId\") OR " +
                "(\"Status\" <> 'Settled' AND \"PaymentEvidence\" IS NULL AND \"SettledAt\" IS NULL AND \"AcquiredVehicleId\" IS NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        // The sale shares the reservation's key and must agree with its vehicle, branch and customer.
        builder.HasOne<Reservation>().WithOne()
            .HasForeignKey<SalesOrder>(item => new { item.Id, item.VehicleId, item.BranchId, item.CustomerId })
            .HasPrincipalKey<Reservation>(item => new { item.Id, item.VehicleId, item.BranchId, item.CustomerId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.OrderNumber).HasMaxLength(20).UseCollation("C");
        builder.HasIndex(item => item.OrderNumber).IsUnique().HasFilter("\"OrderNumber\" IS NOT NULL");
        builder.HasIndex(item => new { item.BranchId, item.Status, item.DueAt });
        builder.HasIndex(item => new { item.BranchId, item.Status, item.UpdatedAt });
        builder.HasIndex(item => new { item.AgentId, item.Status, item.UpdatedAt });
        // One open sale per trade-in car.
        builder.HasIndex(item => item.TradeInAppointmentId).IsUnique()
            .HasFilter($"\"TradeInAppointmentId\" IS NOT NULL AND \"Status\" IN ('Open', {HandledStatuses}, 'Settled')");
        builder.HasIndex(item => item.AcquiredVehicleId).IsUnique().HasFilter("\"AcquiredVehicleId\" IS NOT NULL");
        builder.HasOne<Vehicle>().WithOne().HasForeignKey<SalesOrder>(item => item.AcquiredVehicleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.PaymentEvidence).HasColumnType("jsonb").HasConversion(
            value => value == null ? null : JsonSerializer.Serialize(value, SnapshotJson),
            json => json == null ? null : JsonSerializer.Deserialize<SalePaymentEvidence>(json, SnapshotJson),
            new ValueComparer<SalePaymentEvidence?>((left, right) => left == right,
                value => value == null ? 0 : value.GetHashCode(), value => value));
        builder.Property(item => item.PromoCode).HasMaxLength(PromoCode.CodeMaximumLength).UseCollation("C");
        builder.Property(item => item.WarrantyName).HasMaxLength(WarrantyPackage.NameMaximumLength);
        builder.Property(item => item.TradeInLabel).HasMaxLength(200);
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
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.AgentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.DecidedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PromoCode>().WithMany().HasForeignKey(item => item.PromoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarrantyPackage>().WithMany().HasForeignKey(item => item.WarrantyPackageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IntakeAppointment>().WithMany().HasForeignKey(item => item.TradeInAppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(item => item.IsHandled);
        builder.Ignore(item => item.IsClosed);
    }

    public void Configure(EntityTypeBuilder<SalesOrderEvent> builder)
    {
        builder.ToTable("SalesOrderEvents", table =>
        {
            table.HasCheckConstraint("CK_SalesOrderEvents_Shape",
                "isfinite(\"RecordedAt\") AND \"Sequence\" > 0 AND " +
                "\"Kind\" IN ('Created', 'Claimed', 'Assigned', 'Reassigned', 'Released', 'TermsSaved', 'Submitted', " +
                "'Approved', 'Rejected', 'Cancelled', 'CustomerCancelled', 'Expired', 'Withdrawn', 'Settled') AND " +
                $"\"ToStatus\" IN ({Statuses}) AND (\"FromStatus\" IS NULL OR \"FromStatus\" IN ({Statuses})) AND " +
                "(\"FromStatus\" IS NULL) = (\"Kind\" = 'Created') AND " +
                "(\"ActorId\" IS NULL) = (\"Kind\" IN ('Expired', 'Withdrawn')) AND " +
                "((\"Kind\" IN ('Rejected', 'Cancelled', 'Withdrawn') AND \"Reason\" IS NOT NULL AND " +
                "\"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR " +
                "(\"Kind\" NOT IN ('Rejected', 'Cancelled', 'Withdrawn') AND \"Reason\" IS NULL)) AND " +
                "(\"ReleaseId\" IS NOT NULL) = (\"Kind\" IN ('Cancelled', 'CustomerCancelled', 'Expired', 'Withdrawn')) AND " +
                "(\"Changes\" IS NULL OR (\"Kind\" IN ('TermsSaved', 'Released') AND jsonb_typeof(\"Changes\") = 'array')) AND " +
                "jsonb_typeof(\"Snapshot\") = 'object' AND \"Snapshot\" ->> 'Status' = \"ToStatus\" AND " +
                "(\"Snapshot\" ->> 'Version')::bigint = \"Sequence\"");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.SalesOrderId, item.Sequence }).IsUnique();
        builder.HasIndex(item => item.ReleaseId).IsUnique().HasFilter("\"ReleaseId\" IS NOT NULL");
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.FromStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.ToStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Reason).HasMaxLength(SalesOrderEvent.ReasonMaximumLength);
        builder.Property(item => item.Changes).HasColumnType("jsonb").HasConversion(
            changes => changes == null ? null : JsonSerializer.Serialize(changes, SnapshotJson),
            json => json == null ? null : JsonSerializer.Deserialize<List<SaleChange>>(json, SnapshotJson),
            new ValueComparer<IReadOnlyList<SaleChange>?>((left, right) => ReferenceEquals(left, right),
                value => value == null ? 0 : value.Count, value => value));
        builder.Property(item => item.Snapshot).HasColumnType("jsonb").IsRequired().HasConversion(
            snapshot => JsonSerializer.Serialize(snapshot, SnapshotJson),
            json => JsonSerializer.Deserialize<SaleSnapshot>(json, SnapshotJson)!,
            new ValueComparer<SaleSnapshot>((left, right) => ReferenceEquals(left, right),
                value => value.GetHashCode(), value => value));
        builder.Property(item => item.SettlementReview).HasColumnType("jsonb").HasConversion(
            value => value == null ? null : JsonSerializer.Serialize(value, SnapshotJson),
            json => json == null ? null : JsonSerializer.Deserialize<SaleSettlementReview>(json, SnapshotJson),
            new ValueComparer<SaleSettlementReview?>((left, right) => left == right,
                value => value == null ? 0 : value.GetHashCode(), value => value));
        builder.HasOne<SalesOrder>().WithMany().HasForeignKey(item => item.SalesOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.AgentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.PreviousAgentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReservationRelease>().WithOne().HasForeignKey<SalesOrderEvent>(item => item.ReleaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<DealNumberCounter> builder)
    {
        builder.ToTable("DealNumberCounters", table => table.HasCheckConstraint("CK_DealNumberCounters_Shape",
            "\"Year\" BETWEEN 2000 AND 9999 AND \"LastValue\" BETWEEN 0 AND 999999"));
        builder.HasKey(item => item.Year);
        builder.Property(item => item.Year).ValueGeneratedNever();
    }
}
