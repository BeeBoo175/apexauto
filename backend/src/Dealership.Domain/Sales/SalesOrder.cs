using System.Text.RegularExpressions;
using Dealership.Domain.Vehicles;

namespace Dealership.Domain.Sales;

public sealed record DealReservation(Guid Id, Guid? PromoId, string? PromoCode, decimal PromoDiscount, decimal Deposit);

public sealed record DealWarranty(Guid Id, string Name, decimal Price);

public sealed record DealTradeIn(Guid AppointmentId, decimal Allowance);

// A commercial deal from draft through manager approval. Settlement and handover extend this in later slices.
public sealed partial class SalesOrder
{
    private SalesOrder()
    {
    }

    public Guid Id { get; private set; }

    // Assigned once, at first submission, and kept across resubmissions.
    public string? OrderNumber { get; private set; }

    public Guid VehicleId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid SalesAgentId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid? ReservationId { get; private set; }

    public Guid? PromoId { get; private set; }

    public string? PromoCode { get; private set; }

    public decimal BasePrice { get; private set; }

    public decimal RoadTax { get; private set; }

    public decimal ProcessingFee { get; private set; }

    public decimal SstRate { get; private set; }

    public decimal ServiceTax { get; private set; }

    public decimal PromoDiscount { get; private set; }

    public decimal NegotiatedDiscount { get; private set; }

    public Guid? WarrantyPackageId { get; private set; }

    public string? WarrantyName { get; private set; }

    public decimal WarrantyPrice { get; private set; }

    public decimal DepositCredit { get; private set; }

    public Guid? TradeInAppointmentId { get; private set; }

    public decimal TradeInAllowance { get; private set; }

    public decimal BalancePayable { get; private set; }

    // The agent discount limit in force when the deal was submitted; approval is bound to it.
    public decimal? DiscountLimitAtSubmit { get; private set; }

    public Guid? DecidedById { get; private set; }

    public SalesOrderStatus Status { get; private set; }

    public long Version { get; private set; } = 1;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public static bool ValidNumber(string? number) => number is not null && NumberPattern().IsMatch(number);

    public static SalesOrder CreateDraft(Guid id, Guid vehicleId, Guid customerId, Guid agentId, Guid branchId,
        decimal basePrice, decimal roadTax, PurchaseSettings settings, DealReservation? reservation,
        DateTimeOffset now, out SalesOrderEvent created)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (id == Guid.Empty || vehicleId == Guid.Empty || customerId == Guid.Empty || agentId == Guid.Empty ||
            branchId == Guid.Empty || now.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A deal requires its vehicle, customer, agent, branch and a UTC time.");
        }

        var order = new SalesOrder
        {
            Id = id,
            VehicleId = vehicleId,
            CustomerId = customerId,
            SalesAgentId = agentId,
            BranchId = branchId,
            BasePrice = basePrice,
            RoadTax = roadTax,
            ReservationId = reservation?.Id,
            PromoId = reservation?.PromoId,
            PromoCode = reservation?.PromoCode,
            PromoDiscount = reservation?.PromoDiscount ?? 0m,
            DepositCredit = reservation?.Deposit ?? 0m,
            Status = SalesOrderStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        order.Reprice(settings);
        created = new SalesOrderEvent(id, order.Version, SalesOrderEventKind.Created, null, SalesOrderStatus.Draft,
            agentId, null, now);
        return order;
    }

    // An approved deal that is edited loses its approval and returns to Draft; a pending one cannot be edited.
    public SalesOrderEvent Revise(DealWarranty? warranty, decimal negotiatedDiscount, DealTradeIn? tradeIn,
        PurchaseSettings settings, Guid actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (Status is not (SalesOrderStatus.Draft or SalesOrderStatus.Approved))
        {
            throw new InvalidOperationException("Only a draft or approved deal can be edited.");
        }

        // Price the new terms first so a rejected edit leaves the deal unchanged.
        DealPricing.Calculate(new DealTerms(BasePrice, RoadTax, settings.ProcessingFee, settings.SstRate,
            PromoDiscount, negotiatedDiscount, warranty?.Price ?? 0m, DepositCredit, tradeIn?.Allowance ?? 0m));
        NegotiatedDiscount = negotiatedDiscount;
        WarrantyPackageId = warranty?.Id;
        WarrantyName = warranty?.Name;
        WarrantyPrice = warranty?.Price ?? 0m;
        TradeInAppointmentId = tradeIn?.AppointmentId;
        TradeInAllowance = tradeIn?.Allowance ?? 0m;
        Reprice(settings);
        var from = Status;
        DecidedById = null;
        DiscountLimitAtSubmit = null;
        return Move(SalesOrderEventKind.Revised, from, SalesOrderStatus.Draft, actorId, null, now);
    }

    // Captures the effective settings. A negotiated discount within the limit is approved at once.
    public SalesOrderEvent Submit(PurchaseSettings settings, string number, Guid actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (Status != SalesOrderStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft deal can be submitted.");
        }

        if (OrderNumber is null && !ValidNumber(number) || OrderNumber is not null && number != OrderNumber)
        {
            throw new ArgumentException("A deal keeps the number it was first given.");
        }

        Reprice(settings);
        var within = NegotiatedDiscount <= settings.MaxAgentDiscountAmount;
        var submitted = Move(SalesOrderEventKind.Submitted, SalesOrderStatus.Draft,
            within ? SalesOrderStatus.Approved : SalesOrderStatus.PendingManagerApproval, actorId, null, now);
        OrderNumber = number;
        DiscountLimitAtSubmit = settings.MaxAgentDiscountAmount;
        SubmittedAtUtc = now;
        return submitted;
    }

    public SalesOrderEvent Approve(Guid managerId, DateTimeOffset now)
    {
        if (Status != SalesOrderStatus.PendingManagerApproval)
        {
            throw new InvalidOperationException("Only a pending deal can be approved.");
        }

        if (managerId == Guid.Empty || managerId == SalesAgentId)
        {
            throw new InvalidOperationException("An agent cannot approve their own excess discount.");
        }

        DecidedById = managerId;
        return Move(SalesOrderEventKind.Approved, SalesOrderStatus.PendingManagerApproval, SalesOrderStatus.Approved,
            managerId, null, now);
    }

    public SalesOrderEvent Reject(Guid managerId, string reason, DateTimeOffset now)
    {
        if (Status != SalesOrderStatus.PendingManagerApproval)
        {
            throw new InvalidOperationException("Only a pending deal can be rejected.");
        }

        if (managerId == Guid.Empty || managerId == SalesAgentId)
        {
            throw new InvalidOperationException("An agent cannot decide their own excess discount.");
        }

        DecidedById = null;
        DiscountLimitAtSubmit = null;
        return Move(SalesOrderEventKind.Rejected, SalesOrderStatus.PendingManagerApproval, SalesOrderStatus.Draft,
            managerId, RequiredReason(reason), now);
    }

    public SalesOrderEvent Cancel(Guid actorId, string reason, DateTimeOffset now)
    {
        if (Status == SalesOrderStatus.Cancelled)
        {
            throw new InvalidOperationException("A cancelled deal is final.");
        }

        return Move(SalesOrderEventKind.Cancelled, Status, SalesOrderStatus.Cancelled, actorId,
            RequiredReason(reason), now);
    }

    public static bool ValidReason(string? reason) => reason is not null && reason == reason.Trim() &&
        reason.Length is >= 1 and <= SalesOrderEvent.ReasonMaximumLength && !reason.Any(char.IsControl);

    private static string RequiredReason(string reason) => ValidReason(reason)
        ? reason
        : throw new ArgumentException("A reason of 1 to 1000 trimmed characters is required.");

    private void Reprice(PurchaseSettings settings)
    {
        var price = DealPricing.Calculate(new DealTerms(BasePrice, RoadTax, settings.ProcessingFee, settings.SstRate,
            PromoDiscount, NegotiatedDiscount, WarrantyPrice, DepositCredit, TradeInAllowance));
        ProcessingFee = settings.ProcessingFee;
        SstRate = settings.SstRate;
        ServiceTax = price.ServiceTax;
        BalancePayable = price.BalancePayable;
    }

    private SalesOrderEvent Move(SalesOrderEventKind kind, SalesOrderStatus from, SalesOrderStatus to, Guid actorId,
        string? reason, DateTimeOffset now)
    {
        if (actorId == Guid.Empty || now.Offset != TimeSpan.Zero || now < UpdatedAtUtc)
        {
            throw new ArgumentException("A deal action requires an actor and a UTC time that does not go backwards.");
        }

        Status = to;
        UpdatedAtUtc = now;
        Version++;
        return new SalesOrderEvent(Id, Version, kind, from, to, actorId, reason, now);
    }

    [GeneratedRegex(@"^VPO-[0-9]{4}-[0-9]{6}$")]
    private static partial Regex NumberPattern();
}
