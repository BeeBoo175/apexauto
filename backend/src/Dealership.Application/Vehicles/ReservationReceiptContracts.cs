using System.Globalization;
using Dealership.Domain.Vehicles;
using static Dealership.Application.Vehicles.ReservationReceiptEncoding;

namespace Dealership.Application.Vehicles;

public sealed record ReservationReceipt(Guid Id, Guid VehicleId, Guid BranchId, Guid PublicationId,
    ReservationBuyer Buyer, Guid? PromoId, string? PromoCode, decimal SstRate, PurchasePrice Price,
    decimal Deposit, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string Number = "")
{
    public static ReservationReceipt From(Reservation reservation) => new(reservation.Id, reservation.VehicleId,
        reservation.BranchId, reservation.PublicationId, reservation.Buyer, reservation.PromoId,
        reservation.PromoCode, EncodeDecimal(reservation.SstRate), EncodePrice(reservation.Price), EncodeDecimal(reservation.Deposit),
        reservation.CreatedAt, reservation.ExpiresAt, reservation.Number);
}

public sealed record ReservationRefundReceipt(Guid Id, Guid ReservationId, string Kind, Guid? ActorId,
    string? Reason, DateTimeOffset RecordedAt, decimal Refund)
{
    public static ReservationRefundReceipt From(ReservationRelease release) => new(release.Id, release.ReservationId,
        release.Kind.ToString(), release.ActorId, release.Reason, release.RecordedAt, EncodeDecimal(release.Refund));
}

internal static class ReservationReceiptEncoding
{
    // PostgreSQL numeric scale must not change the JSON of a previously confirmed receipt.
    internal static decimal EncodeDecimal(decimal value) => decimal.Parse(value.ToString("G29", CultureInfo.InvariantCulture),
        NumberStyles.Float, CultureInfo.InvariantCulture);

    internal static PurchasePrice EncodePrice(PurchasePrice price) => new(EncodeDecimal(price.VehiclePrice), EncodeDecimal(price.ProcessingFee),
        EncodeDecimal(price.RoadTax), EncodeDecimal(price.ServiceTax), EncodeDecimal(price.PromoDiscount), EncodeDecimal(price.Total));
}
