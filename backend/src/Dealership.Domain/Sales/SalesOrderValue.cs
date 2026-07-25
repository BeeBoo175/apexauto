using System.Linq.Expressions;

namespace Dealership.Domain.Sales;

public sealed record SalesOrderValue
{
    public Guid Id { get; init; }

    public Guid BranchId { get; init; }

    public SalesOrderStatus Status { get; init; }

    public string? OrderNumber { get; init; }

    public DateTimeOffset? DeliveredAt { get; init; }

    public decimal Subtotal { get; init; }

    public static readonly Expression<Func<SalesOrder, SalesOrderValue>> Projection = order => new SalesOrderValue
    {
        Id = order.Id,
        BranchId = order.BranchId,
        Status = order.Status,
        OrderNumber = order.OrderNumber,
        DeliveredAt = order.DeliveredAt,
        Subtotal = order.BalancePayable + order.DepositCredit + order.TradeInAllowance
    };
}
