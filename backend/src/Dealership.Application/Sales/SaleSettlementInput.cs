using System.Text.Json.Serialization;
using Dealership.Application.Common;
using Dealership.Domain.Sales;

namespace Dealership.Application.Sales;

// Transport amounts use the same exact-sen reader as sale terms before domain evidence is constructed.
public sealed record SaleSettlementInput(Guid OperationId, long ExpectedVersion, SalePaymentInput? Payment,
    SaleSettlementReview? Review)
{
    public SaleSettlementRequest? Command() => Payment is null || Review is null
        ? null
        : new(OperationId, ExpectedVersion, Payment.Evidence(), Review);
}

public sealed record SalePaymentInput(SalePaymentMethod Method,
    [property: JsonConverter(typeof(MyrAmountJsonConverter))] decimal ReceivedAmount,
    string? PaymentReference, DateOnly? PaymentDate, string? Bank,
    [property: JsonConverter(typeof(MyrAmountJsonConverter))] decimal FinancingAmount,
    string? LouReference, bool LouVerified, bool ZeroBalanceConfirmed)
{
    public SalePaymentEvidence Evidence() => new(Method, ReceivedAmount, PaymentReference, PaymentDate, Bank,
        FinancingAmount, LouReference, LouVerified, ZeroBalanceConfirmed);
}
