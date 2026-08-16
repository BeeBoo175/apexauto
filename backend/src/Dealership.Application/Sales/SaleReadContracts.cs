using System.Text.Json.Serialization;
using Dealership.Application.Common;
using Dealership.Domain.Sales;
using Dealership.Domain.Vehicles;

namespace Dealership.Application.Sales;

// The vehicle, customer and reservation facts a sale is about; staff are identified by email.
public sealed record SaleContext(string ReservationNumber, string Registration, string Vehicle, string Vin,
    int MileageKilometres, string Customer, string CustomerMobile, string CustomerEmail, string Branch,
    DateTimeOffset ReservedAt, int? WarrantyMonths, int? WarrantyMileageCapKm, decimal? TradeInMinimum,
    decimal? TradeInMaximum, Guid? CustomerId = null);

// What the reading actor may do next, decided once on the server so every surface offers the same actions.
public sealed record SaleActions(bool Claim, bool Assign, bool Release, bool SaveTerms, bool Submit, bool Decide,
    bool Cancel);

public sealed record SaleView(Guid Id, SaleSnapshot Sale, string? Agent, string? DecidedBy, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, SaleContext Context, SaleActions Actions);

// One row in an agent's queue or a manager's table.
public sealed record SaleSummary(Guid Id, string ReservationNumber, string? OrderNumber, string Status,
    string Registration, int ManufactureYear, string Make, string Model, string Trim, string Customer, string? Agent, string Branch, DateTimeOffset? DueAt,
    long? PausedSeconds, DateTimeOffset? ClaimedAt, decimal NegotiatedDiscount, decimal? DiscountLimitAtSubmit,
    decimal BalancePayable, DateTimeOffset? SubmittedAt, DateTimeOffset? DecidedAt, DateTimeOffset? ClosedAt,
    string? Outcome, long Version, DateTimeOffset UpdatedAt, string? AgentName = null, Guid? AgentPictureId = null,
    Guid? AgentId = null, bool AgentSuspended = false, bool CustomerSuspended = false, Guid? CustomerId = null);

public sealed record SaleQueueCounts(int ToClaim, int Handling, int Waiting, int Settling, int Completed);

public sealed record SaleQueuePage(IReadOnlyList<SaleSummary> Items, int Total, int Page, int PageSize,
    SaleQueueCounts Counts);

public sealed record SaleTableCounts(int NeedsApproval, int Open, int BeingHandled, int Approved, int AwaitingHandover,
    int Delivered, int History, int AwaitingDeposit = 0, int All = 0);

// A car held for a customer who has not yet confirmed the deposit staff asked for.
public sealed record DepositRequestRow(Guid RequestId, Guid VehicleId, string Registration, int ManufactureYear,
    string Make, string Model, string Trim, Guid CustomerId, string Customer, string Branch, string RequestedBy,
    DateTimeOffset ExpiresAtUtc);

// View names the tab actually read, which the server chooses when the request leaves it open.
// The Awaiting deposit tab lists requests instead of sales, so its rows ride in DepositRequests and Items is empty.
public sealed record SaleTablePage(IReadOnlyList<SaleSummary> Items, int Total, int Page, int PageSize,
    SaleTableCounts Counts, string View, IReadOnlyList<DepositRequestRow>? DepositRequests = null);

public sealed record SaleTableInput
{
    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public string? View { get; init; }

    public string? Search { get; init; }

    public string? Sort { get; init; }

    public string? Direction { get; init; }

    public string? Handler { get; init; }

    public string? Branch { get; init; }

    public string? Outcome { get; init; }

    public decimal? MinimumDiscount { get; init; }

    public decimal? MaximumDiscount { get; init; }

    public decimal? MinimumBalance { get; init; }

    public decimal? MaximumBalance { get; init; }

    public string? Registration { get; init; }

    public string? Vehicle { get; init; }

    public string? Customer { get; init; }

    public string? Reference { get; init; }

    public decimal? MinimumHoursLeft { get; init; }

    public decimal? MaximumHoursLeft { get; init; }

    public string? UpdatedFrom { get; init; }

    public string? UpdatedTo { get; init; }
}

// A null view opens the first tab with work, in the order the tabs are shown.
public sealed record SaleTableQuery(int Page, int PageSize, string? View, string? Search, string? Sort, bool Descending)
{
    public string? Agent { get; init; }

    public string? Branch { get; init; }

    public string? Outcome { get; init; }

    public string? Registration { get; init; }

    public string? Vehicle { get; init; }

    public string? Customer { get; init; }

    public string? Reference { get; init; }

    public decimal? MinimumDiscount { get; init; }

    public decimal? MaximumDiscount { get; init; }

    public decimal? MinimumBalance { get; init; }

    public decimal? MaximumBalance { get; init; }

    public ListDateRange? Updated { get; init; }

    public decimal? MinimumHoursLeft { get; init; }

    public decimal? MaximumHoursLeft { get; init; }
}

// One retained history entry; changed terms carry invariant before and after text.
public sealed record SaleHistoryItem(Guid Id, long Version, string Kind, DateTimeOffset RecordedAt, string Actor,
    string? Agent, string? PreviousAgent, string? FromStatus, string ToStatus, string? Reason,
    IReadOnlyList<SaleChange> Changes, decimal? Refund)
{
    public IReadOnlyList<SaleHistoryDocument> Documents { get; init; } = [];

    // The account behind Actor, so a reader can open the person; null for the system.
    public Guid? ActorId { get; init; }
}

public sealed record SaleHistoryDocument(Guid Id, string Kind, string Number);

public sealed record SaleHistoryPage(IReadOnlyList<SaleHistoryItem> Items, int TotalCount, int Page, int PageSize);

// The sale as it stood after one action, with the snapshot before it so changed fields can be shown.
public sealed record SaleVersionView(Guid SaleId, long Version, string Kind, DateTimeOffset RecordedAt, string Actor,
    string? Agent, string? Reason, SaleSnapshot Snapshot, SaleSnapshot? Previous);

public sealed record WarrantyOption(Guid Id, string Name, int DurationMonths, int MileageCapKm, decimal Price,
    string MinimumGrade);

public sealed record TradeInOption(Guid AppointmentId, string Label, decimal Minimum, decimal Maximum);

public sealed record SaleOptions(IReadOnlyList<WarrantyOption> Warranties, IReadOnlyList<TradeInOption> TradeIns);

public sealed record SaleAgent(Guid Id, string Email, string Name);

// Listing content is retained by the reservation; vehicle identity and mileage are current record facts.
public sealed record SaleVehicleReference(Guid SaleId, string Make, string Model, string Trim, int ManufactureYear,
    int MileageKilometres, string BranchName, DateTimeOffset PublishedAt, SaleVehicleListing Listing);

public sealed record SaleVehicleListing(Guid VehicleId, decimal VehiclePrice, decimal RoadTax,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] VehicleTransmission Transmission,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] VehicleFuel Fuel, string Description,
    IReadOnlyList<Guid> PhotoIds);

public sealed record SaleReadResult<T>(SaleResultStatus Status, T? Value = default);

public interface ISaleReadStore
{
    Task<SaleReadResult<SaleVehicleReference>> VehicleAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken);

    Task<SaleReadResult<SaleView>> GetAsync(Guid actorId, Guid saleId, CancellationToken cancellationToken);

    Task<SaleReadResult<SaleOptions>> OptionsAsync(Guid actorId, Guid saleId, CancellationToken cancellationToken);

    Task<SaleReadResult<IReadOnlyList<SaleAgent>>> AgentsAsync(Guid actorId, Guid saleId,
        CancellationToken cancellationToken);

    Task<SaleReadResult<SaleHistoryPage>> HistoryAsync(Guid actorId, Guid saleId, int page, int pageSize,
        CancellationToken cancellationToken);

    Task<SaleReadResult<SaleVersionView>> VersionAsync(Guid actorId, Guid saleId, long version,
        CancellationToken cancellationToken);
}

public interface ISaleListStore
{
    Task<SaleReadResult<SaleQueuePage>> QueueAsync(Guid actorId, string view, string? search, int page, int pageSize,
        CancellationToken cancellationToken);

    Task<SaleReadResult<SaleTablePage>> TableAsync(Guid actorId, SaleTableQuery query,
        CancellationToken cancellationToken);
}
