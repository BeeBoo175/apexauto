using System.Text.Json.Serialization;

namespace Dealership.Domain.Sales;

// The sale exactly as it stood after one action. Its member names match the sale's columns, so the database can
// record the same shape for the actions it takes on its own; the history table's append-only guard keeps it fixed.
public sealed record SaleSnapshot(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] SalesOrderStatus Status,
    Guid? AgentId, DateTimeOffset? ClaimedAt, DateTimeOffset? DueAt, long? PausedSeconds, string? OrderNumber,
    string? PromoCode, decimal BasePrice, decimal RoadTax, decimal ProcessingFee, decimal SstRate, decimal ServiceTax,
    decimal PromoDiscount, decimal NegotiatedDiscount, Guid? WarrantyPackageId, string? WarrantyName,
    decimal WarrantyPrice, decimal DepositCredit, Guid? TradeInAppointmentId, string? TradeInLabel,
    decimal TradeInAllowance, decimal BalancePayable, DateTimeOffset? SubmittedAt, decimal? DiscountLimitAtSubmit,
    Guid? DecidedById, DateTimeOffset? DecidedAt, DateTimeOffset? ClosedAt, long Version,
    SalePaymentEvidence? PaymentEvidence = null, DateTimeOffset? SettledAt = null, Guid? AcquiredVehicleId = null)
{
    public static SaleSnapshot Of(SalesOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return new(order.Status, order.AgentId, order.ClaimedAt, order.DueAt, order.PausedSeconds, order.OrderNumber,
            order.PromoCode, order.BasePrice, order.RoadTax, order.ProcessingFee, order.SstRate, order.ServiceTax,
            order.PromoDiscount, order.NegotiatedDiscount, order.WarrantyPackageId, order.WarrantyName,
            order.WarrantyPrice, order.DepositCredit, order.TradeInAppointmentId, order.TradeInLabel,
            order.TradeInAllowance, order.BalancePayable, order.SubmittedAt, order.DiscountLimitAtSubmit,
            order.DecidedById, order.DecidedAt, order.ClosedAt, order.Version,
            order.PaymentEvidence, order.SettledAt, order.AcquiredVehicleId);
    }
}
