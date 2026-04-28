using Dealership.Application.Common;
using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

// The deal identity doubles as the operation identity, so a lost response can be replayed without a second deal.
public sealed record DealClaimRequest(Guid Id);

// A manager gives a hold to an agent: the first assignment starts the deal, later ones move it to another agent.
public sealed record DealAssignRequest(Guid Id, Guid AgentId);

public sealed record DealReleaseRequest();

public sealed record DealReviseRequest(long ExpectedVersion, Guid? WarrantyPackageId, decimal NegotiatedDiscount,
    Guid? TradeInAppointmentId, decimal TradeInAllowance);

public sealed record DealTransitionRequest(long ExpectedVersion, string? Reason);

public enum DealStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict,
    Unavailable
}

public sealed record DealEventView(Guid Id, string Kind, string? FromStatus, string ToStatus, Guid ActorId,
    string? Reason, DateTimeOffset RecordedAtUtc)
{
    public static DealEventView From(SalesOrderEvent item) => new(item.Id, item.Kind.ToString(),
        item.FromStatus?.ToString(), item.ToStatus.ToString(), item.ActorId, item.Reason, item.RecordedAtUtc);
}

// Names and contact the drawer needs beside the ids; staff identify people by email.
public sealed record DealContext(string Registration, string Vehicle, string Vin, int MileageKilometres,
    string Customer, string CustomerMobile, string CustomerEmail, string Agent, string Branch,
    string? TradeIn, decimal? TradeInMinimum, decimal? TradeInMaximum,
    int? WarrantyMonths, int? WarrantyMileageCapKm,
    DateTimeOffset? HoldCreatedAtUtc, DateTimeOffset? HoldExpiresAtUtc,
    IReadOnlyDictionary<Guid, string> Staff);

public sealed record DealView(Guid Id, string? OrderNumber, string Status, Guid VehicleId, Guid CustomerId,
    Guid SalesAgentId, Guid BranchId, Guid? ReservationId, string? PromoCode, decimal BasePrice, decimal PromoDiscount,
    decimal NegotiatedDiscount, decimal ProcessingFee, decimal RoadTax, decimal SstRate, decimal ServiceTax,
    Guid? WarrantyPackageId, string? WarrantyName, decimal WarrantyPrice, decimal DepositCredit,
    Guid? TradeInAppointmentId, decimal TradeInAllowance, decimal BalancePayable, decimal? DiscountLimitAtSubmit,
    Guid? DecidedById, long Version, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<DealEventView> Events)
{
    public DealContext? Context { get; init; }

    public static DealView From(SalesOrder order, IEnumerable<SalesOrderEvent> events) => new(order.Id,
        order.OrderNumber, order.Status.ToString(), order.VehicleId, order.CustomerId, order.SalesAgentId,
        order.BranchId, order.ReservationId, order.PromoCode, order.BasePrice, order.PromoDiscount,
        order.NegotiatedDiscount, order.ProcessingFee, order.RoadTax, order.SstRate, order.ServiceTax,
        order.WarrantyPackageId, order.WarrantyName, order.WarrantyPrice, order.DepositCredit,
        order.TradeInAppointmentId, order.TradeInAllowance, order.BalancePayable, order.DiscountLimitAtSubmit,
        order.DecidedById, order.Version, order.CreatedAtUtc, order.UpdatedAtUtc,
        events.OrderBy(item => item.Sequence).Select(DealEventView.From).ToList());
}

// Plain-language next step, computed once on the server so every role reads the same wording.
public static class DealNextStep
{
    public static string For(SalesOrderStatus status, bool previouslySubmitted) => status switch
    {
        SalesOrderStatus.Draft => previouslySubmitted ? "Edit and resubmit" : "Add terms and submit",
        SalesOrderStatus.PendingManagerApproval => "Awaiting manager approval",
        SalesOrderStatus.Approved => "Approved",
        _ => "Cancelled"
    };
}

public sealed record DealSummary(Guid Id, string? OrderNumber, string Status, string NextStep, Guid VehicleId,
    string Registration, int ManufactureYear, string Make, string Model, string Trim, Guid CustomerId, string Customer, Guid SalesAgentId, string Agent,
    Guid BranchId, string Branch, decimal BalancePayable, decimal NegotiatedDiscount, decimal? DiscountLimitAtSubmit,
    long Version, DateTimeOffset? SubmittedAtUtc, DateTimeOffset UpdatedAtUtc);

// Counts for the role's scope, shown on the view tabs regardless of the active view.
public sealed record DealCounts(int Active, int NeedsApproval, int History);

public sealed record DealPage(IReadOnlyList<DealSummary> Items, int Total, int Page, int PageSize, DealCounts Counts);

public sealed record WarrantyOption(Guid Id, string Name, int DurationMonths, int MileageCapKm, decimal Price,
    string MinimumGrade);

public sealed record TradeInOption(Guid AppointmentId, string Label, decimal Minimum, decimal Maximum);

public sealed record DealOptions(IReadOnlyList<WarrantyOption> Warranties, IReadOnlyList<TradeInOption> TradeIns);

// Conflict carries the current deal when it is known so a stale editor can reload.
// One retained receipt in a hold's or deal's history; staff are identified by email and the system by no actor.
public sealed record HoldHistoryItem(Guid Id, string Source, string Kind, DateTimeOffset RecordedAtUtc, Guid? ActorId,
    string Actor, string? Agent, string? PreviousAgent, string? Reason, string? FromStatus, string? ToStatus,
    IReadOnlyList<DealChange> Changes);

public sealed record HoldHistoryPage(IReadOnlyList<HoldHistoryItem> Items, int Total, int Page, int PageSize);

public sealed record DealResult(DealStatus Status, DealView? Deal = null, DealPage? Page = null,
    DealOptions? Options = null, HoldHistoryPage? History = null);

public sealed record DealListQuery(int Page, int PageSize, string View, string? Status, string? Sort, bool Descending,
    string? Number, string? Vehicle, string? Customer, string? Search)
{
    // Column filters beyond the identifying text columns.
    public string? Agent { get; init; }

    public string? Branch { get; init; }

    public string? Registration { get; init; }

    public string? NextStep { get; init; }

    public decimal? MinimumBalance { get; init; }

    public decimal? MaximumBalance { get; init; }

    public decimal? MinimumDiscount { get; init; }

    public decimal? MaximumDiscount { get; init; }

    public ListDateRange? Submitted { get; init; }

    public ListDateRange? Updated { get; init; }
}

// The extra column filters as they arrive from the request, before validation.
public sealed record DealColumnFilters(string? Agent, string? Branch, string? NextStep, decimal? MinimumBalance,
    decimal? MaximumBalance, decimal? MinimumDiscount, decimal? MaximumDiscount, string? SubmittedFrom,
    string? SubmittedTo, string? UpdatedFrom, string? UpdatedTo, string? Registration = null);

public interface IDealStore
{
    Task<DealResult> ClaimAsync(Guid actorId, Guid reservationId, DealClaimRequest request, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<DealResult> AssignAsync(Guid actorId, Guid reservationId, DealAssignRequest request, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<DealResult> ReleaseAsync(Guid actorId, Guid reservationId, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<DealResult> ReviseAsync(Guid actorId, Guid dealId, DealReviseRequest request, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<DealResult> TransitionAsync(Guid actorId, Guid dealId, DealAction action, DealTransitionRequest request,
        DateTimeOffset now, CancellationToken cancellationToken);

    Task<DealResult> GetAsync(Guid actorId, Guid dealId, CancellationToken cancellationToken);

    Task<DealResult> ListAsync(Guid actorId, DealListQuery query, CancellationToken cancellationToken);

    Task<DealResult> OptionsAsync(Guid actorId, Guid dealId, CancellationToken cancellationToken);

    // One history for a hold and the deal built on it, read from either side.
    Task<DealResult> HistoryAsync(Guid actorId, Guid id, bool ofDeal, int page, int pageSize,
        CancellationToken cancellationToken);
}

public enum DealAction
{
    Submit,
    Approve,
    Reject,
    Cancel
}
