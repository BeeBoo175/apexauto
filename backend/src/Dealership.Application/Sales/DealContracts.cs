using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

public sealed record DealCreateRequest(Guid Id, Guid VehicleId, Guid? ReservationId, string? CustomerEmail);

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

public sealed record DealView(Guid Id, string? OrderNumber, string Status, Guid VehicleId, Guid CustomerId,
    Guid SalesAgentId, Guid BranchId, Guid? ReservationId, string? PromoCode, decimal BasePrice, decimal PromoDiscount,
    decimal NegotiatedDiscount, decimal ProcessingFee, decimal RoadTax, decimal SstRate, decimal ServiceTax,
    Guid? WarrantyPackageId, string? WarrantyName, decimal WarrantyPrice, decimal DepositCredit,
    Guid? TradeInAppointmentId, decimal TradeInAllowance, decimal BalancePayable, decimal? DiscountLimitAtSubmit,
    Guid? DecidedById, long Version, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<DealEventView> Events)
{
    public static DealView From(SalesOrder order, IEnumerable<SalesOrderEvent> events) => new(order.Id,
        order.OrderNumber, order.Status.ToString(), order.VehicleId, order.CustomerId, order.SalesAgentId,
        order.BranchId, order.ReservationId, order.PromoCode, order.BasePrice, order.PromoDiscount,
        order.NegotiatedDiscount, order.ProcessingFee, order.RoadTax, order.SstRate, order.ServiceTax,
        order.WarrantyPackageId, order.WarrantyName, order.WarrantyPrice, order.DepositCredit,
        order.TradeInAppointmentId, order.TradeInAllowance, order.BalancePayable, order.DiscountLimitAtSubmit,
        order.DecidedById, order.Version, order.CreatedAtUtc, order.UpdatedAtUtc,
        events.OrderBy(item => item.Sequence).Select(DealEventView.From).ToList());
}

public sealed record DealSummary(Guid Id, string? OrderNumber, string Status, Guid VehicleId, Guid CustomerId,
    Guid SalesAgentId, Guid BranchId, decimal BalancePayable, long Version, DateTimeOffset UpdatedAtUtc);

public sealed record DealPage(IReadOnlyList<DealSummary> Items, int Total, int Page, int PageSize);

// Conflict carries the current deal when it is known so a stale editor can reload.
public sealed record DealResult(DealStatus Status, DealView? Deal = null, DealPage? Page = null);

public interface IDealStore
{
    Task<DealResult> CreateAsync(Guid actorId, DealCreateRequest request, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<DealResult> ReviseAsync(Guid actorId, Guid dealId, DealReviseRequest request, DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<DealResult> TransitionAsync(Guid actorId, Guid dealId, DealAction action, DealTransitionRequest request,
        DateTimeOffset now, CancellationToken cancellationToken);

    Task<DealResult> GetAsync(Guid actorId, Guid dealId, CancellationToken cancellationToken);

    Task<DealResult> ListAsync(Guid actorId, int page, int pageSize, string? status, string? number,
        CancellationToken cancellationToken);
}

public enum DealAction
{
    Submit,
    Approve,
    Reject,
    Cancel
}
