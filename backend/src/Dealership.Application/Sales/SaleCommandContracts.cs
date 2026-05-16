using Dealership.Application.Common;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;
using System.Text.Json.Serialization;

namespace Dealership.Application.Sales;

public enum SaleResultStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    Conflict,
    Unavailable
}

// Every command names its operation and the sale version it was based on, so a lost response can be replayed exactly.
public sealed record SaleCommandRequest(Guid OperationId, long ExpectedVersion);

public sealed record SaleAssignmentRequest(Guid OperationId, long ExpectedVersion, Guid AgentId);

public sealed record SaleTermsRequest(Guid OperationId, long ExpectedVersion, Guid? WarrantyPackageId,
    [property: JsonConverter(typeof(MyrAmountJsonConverter))] decimal NegotiatedDiscount, Guid? TradeInAppointmentId,
    [property: JsonConverter(typeof(MyrAmountJsonConverter))] decimal TradeInAllowance);

public sealed record SaleReasonRequest(Guid OperationId, long ExpectedVersion, string? Reason);

public sealed record SaleSettlementRequest(Guid OperationId, long ExpectedVersion, SalePaymentEvidence Payment,
    SaleSettlementReview Review);

public sealed record SaleHandoverRequest(Guid OperationId, long ExpectedVersion, SaleHandoverEvidence Evidence);

public sealed record SaleReceipt(Guid OperationId, Guid SaleId, string Kind, long Version, string Status,
    DateTimeOffset RecordedAt)
{
    public static SaleReceipt From(SalesOrderEvent entry) => new(entry.Id, entry.SalesOrderId, entry.Kind.ToString(),
        entry.Sequence, entry.ToStatus.ToString(), entry.RecordedAt);
}

public sealed record SaleCommandResult(SaleResultStatus Status, SaleReceipt? Receipt = null);

public sealed record SaleStaff(Guid Id, bool IsActive, string Role, Guid? BranchId);

// A trade-in the sale's customer owns, with the valuation ceiling the allowance may not exceed.
public sealed record SaleTradeInChoice(Guid AppointmentId, string Label, decimal Maximum, bool TakenByAnotherSale);

public sealed record SaleCommandContext(SaleStaff? Actor, bool BranchActive, SalesOrder? Sale,
    SalesOrderEvent? Existing, DateTimeOffset Now);

public interface ISaleCommandTransaction : IAsyncDisposable
{
    SaleCommandContext Context { get; }

    Task<SaleStaff?> LockStaffAsync(Guid staffId, CancellationToken cancellationToken);

    Task<PurchaseSettings?> LockSettingsAsync(CancellationToken cancellationToken);

    // An active package the vehicle's signed grade qualifies for, or null.
    Task<SaleWarranty?> ResolveWarrantyAsync(Guid packageId, CancellationToken cancellationToken);

    Task<SaleTradeInChoice?> ResolveTradeInAsync(Guid appointmentId, CancellationToken cancellationToken);

    Task<string> NextOrderNumberAsync(int year, CancellationToken cancellationToken);

    Task<int> ReadVehicleMileageAsync(CancellationToken cancellationToken);

    Task<Guid?> LockTradeInVehicleAsync(CancellationToken cancellationToken);

    Task<bool> ReviewSettlementAsync(SaleSettlementReview review, CancellationToken cancellationToken);

    Task<SaleDocumentCorrectionRecord?> ReadCorrectionAsync(Guid operationId, CancellationToken cancellationToken);

    Task<SaleCorrectionDocument?> ReadCorrectionDocumentAsync(Guid documentId, CancellationToken cancellationToken);

    Task<SaleDocumentCorrectionResult> CommitCorrectionAsync(Guid actorId, SaleDocumentCorrectionRequest request,
        SaleDocumentContents contents, CancellationToken cancellationToken);

    // Records the reservation's full simulated refund before the sale changes, returning its receipt identity, or
    // null when the database refuses it because the hold was released meanwhile.
    Task<Guid?> ReleaseReservationAsync(ReservationReleaseKind kind, Guid actorId, string reason,
        CancellationToken cancellationToken);

    Task<SaleResultStatus> CommitAsync(SalesOrderEvent entry, CancellationToken cancellationToken);
}

public interface ISaleCommandStore
{
    // Locks the actor, the vehicle and the sale in the order every reservation command uses, after releasing an
    // expired or no longer sellable hold, so each decision sees the authoritative state.
    Task<ISaleCommandTransaction> LockAsync(Guid actorId, Guid saleId, Guid operationId,
        CancellationToken cancellationToken);
}
