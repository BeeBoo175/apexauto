using System.Text.RegularExpressions;
using Dealership.Domain.Common;
using Dealership.Domain.Vehicles;
using static Dealership.Domain.Sales.SaleCommandRules;

namespace Dealership.Domain.Sales;

// One sale per reservation, retaining its commercial lifecycle and approved settlement evidence.
// Every action returns the history entry that records it; the entry carries the sale as it stood afterwards.
public sealed partial class SalesOrder
{
    public const int MaximumHoldYears = 10;

    private static readonly SalesOrderStatus[] Handled =
        [SalesOrderStatus.Handling, SalesOrderStatus.PendingApproval, SalesOrderStatus.Approved];

    private SalesOrder()
    {
    }

    // Shares the reservation's identity, so a hold and its sale are always the same record to staff.
    public Guid Id { get; private set; }

    public Guid VehicleId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid CustomerId { get; private set; }

    public SalesOrderStatus Status { get; private set; }

    public Guid? AgentId { get; private set; }

    public DateTimeOffset? ClaimedAt { get; private set; }

    // The running deadline while the sale is open; a handled sale has none and keeps its time in PausedSeconds.
    public DateTimeOffset? DueAt { get; private set; }

    public long? PausedSeconds { get; private set; }

    public string? OrderNumber { get; private set; }

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

    public string? TradeInLabel { get; private set; }

    public decimal TradeInAllowance { get; private set; }

    public decimal BalancePayable { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    // The agent discount limit captured by the latest submission; an approval is bound to it.
    public decimal? DiscountLimitAtSubmit { get; private set; }

    public Guid? DecidedById { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long Version { get; private set; }

    public SalePaymentEvidence? PaymentEvidence { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    public Guid? AcquiredVehicleId { get; private set; }

    public SaleHandoverEvidence? HandoverEvidence { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    // An Administrator's preservation hold keeps the sale's personal details until the date passes. It never advances
    // the sale's version or history, so the database admits it as a change of these three columns alone.
    public DateTimeOffset? RetentionHoldUntil { get; private set; }

    public string? RetentionHoldReason { get; private set; }

    public Guid? RetentionHoldById { get; private set; }

    public bool IsHandled => Handled.Contains(Status);

    public bool IsClosed => IsClosedStatus(Status);

    // Closed sales take no further lifecycle commands; queries mirror this where EF must translate it.
    public static bool IsClosedStatus(SalesOrderStatus status) => status is SalesOrderStatus.Expired or SalesOrderStatus.Cancelled or SalesOrderStatus.Delivered;

    public static bool ValidNumber(string? number) => number is not null && NumberPattern().IsMatch(number);

    // Opens the sale for a reservation as it is confirmed, carrying its agreed price, promo, deposit and deadline.
    public static SalesOrder Open(Reservation reservation, Guid operationId, out SalesOrderEvent created)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (reservation.Id == Guid.Empty || operationId == Guid.Empty || reservation.CreatedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("A sale opens from a confirmed reservation and an operation identity.");
        }

        var order = new SalesOrder
        {
            Id = reservation.Id,
            VehicleId = reservation.VehicleId,
            BranchId = reservation.BranchId,
            CustomerId = reservation.CustomerId,
            Status = SalesOrderStatus.Open,
            DueAt = reservation.ExpiresAt,
            PromoId = reservation.PromoId,
            PromoCode = reservation.PromoCode,
            BasePrice = reservation.Price.VehiclePrice,
            RoadTax = reservation.Price.RoadTax,
            ProcessingFee = reservation.Price.ProcessingFee,
            SstRate = reservation.SstRate,
            PromoDiscount = reservation.Price.PromoDiscount,
            DepositCredit = reservation.Deposit,
            CreatedAt = reservation.CreatedAt,
            UpdatedAt = reservation.CreatedAt
        };
        order.Reprice(order.ProcessingFee, order.SstRate);
        order.Version = 1;
        created = new SalesOrderEvent(operationId, order, SalesOrderEventKind.Created, null, reservation.CustomerId,
            null, null, null, reservation.CreatedAt);
        return order;
    }

    public bool HasExpired(DateTimeOffset now) => Status == SalesOrderStatus.Open && now >= DueAt;

    // Review uses the approved balance rather than repricing from settings or applying its credits again.
    public SalePaymentEvidence ReviewSettlement(SalePaymentEvidence evidence, Guid agentId)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Require(Status == SalesOrderStatus.Approved, "Only an approved sale can be settled.");
        Require(agentId != Guid.Empty && agentId == AgentId, "Only the handling agent confirms settlement.");
        return evidence.Normalize(BalancePayable);
    }

    public SalesOrderEvent Settle(Guid operationId, SalePaymentEvidence evidence, Guid? acquiredVehicleId,
        Guid agentId, DateTimeOffset now)
    {
        RequireCommand(operationId, agentId, now, UpdatedAt);
        var payment = ReviewSettlement(evidence, agentId);
        Require((TradeInAppointmentId is null) == (acquiredVehicleId is null) &&
            acquiredVehicleId != Guid.Empty && acquiredVehicleId != VehicleId,
            "Settlement acquires exactly the linked trade-in vehicle.");
        PaymentEvidence = payment;
        SettledAt = now;
        AcquiredVehicleId = acquiredVehicleId;
        PausedSeconds = null;
        return Record(operationId, SalesOrderEventKind.Settled, SalesOrderStatus.Settled, agentId, now, null, null);
    }

    public SalesOrderEvent Deliver(Guid operationId, SaleHandoverEvidence evidence, int recordedMileage,
        Guid agentId, DateTimeOffset now)
    {
        RequireCommand(operationId, agentId, now, UpdatedAt);
        ArgumentNullException.ThrowIfNull(evidence);
        Require(Status == SalesOrderStatus.Settled, "Only a settled sale can be delivered.");
        Require(agentId != Guid.Empty && agentId == AgentId, "Only the handling agent completes handover.");
        HandoverEvidence = evidence.Validate(recordedMileage);
        DeliveredAt = now;
        ClosedAt = now;
        return Record(operationId, SalesOrderEventKind.Delivered, SalesOrderStatus.Delivered, agentId, now, null, null);
    }

    // An agent claims an open sale for themselves, or a manager assigns it; either way the timer pauses.
    public SalesOrderEvent Claim(Guid operationId, Guid agentId, Guid actorId, DateTimeOffset now)
    {
        RequireCommand(operationId, actorId, now, UpdatedAt);
        RequireAgent(agentId);
        Require(Status == SalesOrderStatus.Open, "Only an open sale can be claimed.");
        Require(!HasExpired(now), "An expired sale cannot be claimed.");
        var paused = (long)Math.Floor((DueAt!.Value - now).TotalSeconds);
        Require(paused >= 1, "An expired sale cannot be claimed.");
        AgentId = agentId;
        ClaimedAt = now;
        PausedSeconds = paused;
        DueAt = null;
        var kind = agentId == actorId ? SalesOrderEventKind.Claimed : SalesOrderEventKind.Assigned;
        return Record(operationId, kind, SalesOrderStatus.Handling, actorId, now, null, null);
    }

    // A manager moves a handled sale to another agent; its terms and status stay as they are.
    public SalesOrderEvent Reassign(Guid operationId, Guid agentId, Guid actorId, DateTimeOffset now)
    {
        RequireCommand(operationId, actorId, now, UpdatedAt);
        RequireAgent(agentId);
        Require(IsHandled || Status == SalesOrderStatus.Settled, "Only a sale awaiting completion can be reassigned.");
        Require(agentId != AgentId, "The sale is already with that agent.");
        Require(agentId != DecidedById, "The manager who approved the sale cannot be its agent.");
        var previous = AgentId;
        AgentId = agentId;
        ClaimedAt = now;
        return Record(operationId, SalesOrderEventKind.Reassigned, Status, actorId, now, null, null, previous);
    }

    // Gives the sale back with the time it had left and the reservation's own terms; earlier terms stay in history.
    public SalesOrderEvent Release(Guid operationId, Guid actorId, DateTimeOffset now)
    {
        RequireCommand(operationId, actorId, now, UpdatedAt);
        Require(Status == SalesOrderStatus.Handling, "Only a sale in handling can be released.");
        var changes = SaleTermsChanges.Between(this, new SaleTermsInput(null, 0m, null));
        var previous = AgentId;
        ApplyTerms(new SaleTermsInput(null, 0m, null));
        Reprice(ProcessingFee, SstRate);
        DueAt = now.AddSeconds(PausedSeconds!.Value);
        AgentId = null;
        ClaimedAt = null;
        PausedSeconds = null;
        DiscountLimitAtSubmit = null;
        DecidedById = null;
        DecidedAt = null;
        return Record(operationId, SalesOrderEventKind.Released, SalesOrderStatus.Open, actorId, now, null, changes,
            previous);
    }

    // Saving terms prices them with the current fee and tax; saving an approved sale withdraws its approval.
    public SalesOrderEvent SaveTerms(Guid operationId, SaleTermsInput terms, PurchaseSettings settings, Guid actorId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(settings);
        RequireCommand(operationId, actorId, now, UpdatedAt);
        Require(Status is SalesOrderStatus.Handling or SalesOrderStatus.Approved,
            "Terms can be saved only while the sale is handled or approved.");
        // Price first so rejected terms leave the sale unchanged.
        Price(terms, settings.ProcessingFee, settings.SstRate);
        var changes = SaleTermsChanges.Between(this, terms);
        Require(changes.Count > 0, "The terms are unchanged.");
        ApplyTerms(terms);
        Reprice(settings.ProcessingFee, settings.SstRate);
        DiscountLimitAtSubmit = null;
        DecidedById = null;
        DecidedAt = null;
        return Record(operationId, SalesOrderEventKind.TermsSaved, SalesOrderStatus.Handling, actorId, now, null,
            changes);
    }

    // Captures the current fee, tax and discount limit. A discount within the limit is approved as it is submitted.
    public SalesOrderEvent Submit(Guid operationId, PurchaseSettings settings, string number, Guid actorId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RequireCommand(operationId, actorId, now, UpdatedAt);
        Require(Status == SalesOrderStatus.Handling, "Only a sale in handling can be submitted.");
        Require(actorId == AgentId, "Only the handling agent submits the sale.");
        if (OrderNumber is null && !ValidNumber(number) || OrderNumber is not null && number != OrderNumber)
        {
            throw new ArgumentException("A sale keeps the number it was first given.");
        }

        Reprice(settings.ProcessingFee, settings.SstRate);
        var within = NegotiatedDiscount <= settings.MaxAgentDiscountAmount;
        OrderNumber = number;
        SubmittedAt = now;
        DiscountLimitAtSubmit = settings.MaxAgentDiscountAmount;
        var next = within ? SalesOrderStatus.Approved : SalesOrderStatus.PendingApproval;
        if (within)
        {
            DecidedAt = now;
        }

        return Record(operationId, SalesOrderEventKind.Submitted, next, actorId, now, null, null);
    }

    public SalesOrderEvent Approve(Guid operationId, Guid managerId, DateTimeOffset now)
    {
        RequireCommand(operationId, managerId, now, UpdatedAt);
        Require(Status == SalesOrderStatus.PendingApproval, "Only a sale awaiting approval can be approved.");
        Require(managerId != AgentId, "An agent cannot approve their own excess discount.");
        DecidedById = managerId;
        DecidedAt = now;
        return Record(operationId, SalesOrderEventKind.Approved, SalesOrderStatus.Approved, managerId, now, null, null);
    }

    // A rejected sale returns to its agent with its terms, to be revised and submitted again.
    public SalesOrderEvent Reject(Guid operationId, Guid managerId, string reason, DateTimeOffset now)
    {
        RequireCommand(operationId, managerId, now, UpdatedAt);
        Require(Status == SalesOrderStatus.PendingApproval, "Only a sale awaiting approval can be rejected.");
        Require(managerId != AgentId, "An agent cannot decide their own excess discount.");
        DiscountLimitAtSubmit = null;
        return Record(operationId, SalesOrderEventKind.Rejected, SalesOrderStatus.Handling, managerId, now,
            RequiredReason(reason), null);
    }

    // Staff cancel with a reason; the caller records the reservation's refund receipt in the same transaction.
    public SalesOrderEvent Cancel(Guid operationId, Guid actorId, string reason, Guid releaseId, DateTimeOffset now)
    {
        RequireCommand(operationId, actorId, now, UpdatedAt);
        Require(!IsClosed && Status != SalesOrderStatus.Settled, "A settled or closed sale cannot be cancelled.");
        return Close(operationId, SalesOrderEventKind.Cancelled, SalesOrderStatus.Cancelled, actorId,
            RequiredReason(reason), releaseId, now);
    }

    // The customer may cancel online only while nobody handles the sale.
    public SalesOrderEvent CancelByCustomer(Guid operationId, Guid customerId, Guid releaseId, DateTimeOffset now)
    {
        RequireCommand(operationId, customerId, now, UpdatedAt);
        Require(Status == SalesOrderStatus.Open && !HasExpired(now), "Only an open sale can be cancelled online.");
        Require(customerId == CustomerId, "Only the reserving customer can cancel online.");
        return Close(operationId, SalesOrderEventKind.CustomerCancelled, SalesOrderStatus.Cancelled, customerId, null,
            releaseId, now);
    }

    public static bool ValidReason(string? reason) => reason is not null && reason == reason.Trim() &&
        reason.Length is >= 1 and <= SalesOrderEvent.ReasonMaximumLength && !reason.Any(char.IsControl);

    private SalesOrderEvent Close(Guid operationId, SalesOrderEventKind kind, SalesOrderStatus status, Guid actorId,
        string? reason, Guid releaseId, DateTimeOffset now)
    {
        if (releaseId == Guid.Empty)
        {
            throw new ArgumentException("A closed sale records the refund receipt it released.");
        }

        var previous = AgentId;
        ClosedAt = now;
        AgentId = null;
        ClaimedAt = null;
        PausedSeconds = null;
        return Record(operationId, kind, status, actorId, now, reason, null, previous, releaseId);
    }

    // The hold must lie in the future and at most ten years ahead, and it carries the Administrator's reason.
    public static bool ValidHold(DateTimeOffset until, string? reason, DateTimeOffset now) =>
        until > now && until <= now.AddYears(MaximumHoldYears) && BoundedText.IsValidRequired(reason);

    public void PlaceRetentionHold(DateTimeOffset until, string reason, Guid actorId, DateTimeOffset now)
    {
        if (actorId == Guid.Empty || !ValidHold(until, reason, now))
        {
            throw new ArgumentException("A hold needs a future date within ten years, a reason and an Administrator.");
        }

        RetentionHoldUntil = until;
        RetentionHoldReason = reason.Trim();
        RetentionHoldById = actorId;
    }

    public void ReleaseRetentionHold()
    {
        RetentionHoldUntil = null;
        RetentionHoldReason = null;
        RetentionHoldById = null;
    }

    private SalesOrderEvent Record(Guid operationId, SalesOrderEventKind kind, SalesOrderStatus next, Guid actorId,
        DateTimeOffset now, string? reason, IReadOnlyList<SaleChange>? changes, Guid? previousAgentId = null,
        Guid? releaseId = null)
    {
        RequireCommand(operationId, actorId, now, UpdatedAt);
        var from = Status;
        Status = next;
        UpdatedAt = now;
        Version++;
        return new SalesOrderEvent(operationId, this, kind, from, actorId, previousAgentId, reason, changes, now)
        {
            ReleaseId = releaseId
        };
    }

    private SalePrice Price(SaleTermsInput terms, decimal processingFee, decimal sstRate) =>
        SalePricing.Calculate(new SaleTerms(BasePrice, RoadTax, processingFee, sstRate, PromoDiscount,
            terms.NegotiatedDiscount, terms.Warranty?.Price ?? 0m, DepositCredit, terms.TradeIn?.Allowance ?? 0m));

    private void Reprice(decimal processingFee, decimal sstRate)
    {
        var price = Price(new SaleTermsInput(
            WarrantyPackageId is { } warrantyId ? new SaleWarranty(warrantyId, WarrantyName!, WarrantyPrice) : null,
            NegotiatedDiscount,
            TradeInAppointmentId is { } appointmentId ? new SaleTradeIn(appointmentId, TradeInLabel!, TradeInAllowance) : null),
            processingFee, sstRate);
        ProcessingFee = processingFee;
        SstRate = sstRate;
        ServiceTax = price.ServiceTax;
        BalancePayable = price.BalancePayable;
    }

    private void ApplyTerms(SaleTermsInput terms)
    {
        NegotiatedDiscount = terms.NegotiatedDiscount;
        WarrantyPackageId = terms.Warranty?.Id;
        WarrantyName = terms.Warranty?.Name;
        WarrantyPrice = terms.Warranty?.Price ?? 0m;
        TradeInAppointmentId = terms.TradeIn?.AppointmentId;
        TradeInLabel = terms.TradeIn?.Label;
        TradeInAllowance = terms.TradeIn?.Allowance ?? 0m;
    }

    [GeneratedRegex(@"^VPO-[0-9]{4}-[0-9]{6}$")]
    private static partial Regex NumberPattern();
}
