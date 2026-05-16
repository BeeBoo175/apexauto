namespace Dealership.Domain.Sales;

public sealed record SaleWarranty(Guid Id, string Name, decimal Price);

// The label names the trade-in car (plate, year, make and model) as it was when the terms were saved.
public sealed record SaleTradeIn(Guid AppointmentId, string Label, decimal Allowance);

// The negotiated part of a sale; the base price, road tax, promo and deposit always come from the reservation.
public sealed record SaleTermsInput(SaleWarranty? Warranty, decimal NegotiatedDiscount, SaleTradeIn? TradeIn);

internal static class SaleTermsChanges
{
    // The terms a change touches, with invariant before and after text, so history can say "from X to Y".
    public static List<SaleChange> Between(SalesOrder order, SaleTermsInput terms)
    {
        var changes = new List<SaleChange>();
        if (terms.NegotiatedDiscount != order.NegotiatedDiscount)
        {
            changes.Add(new(SaleChange.NegotiatedDiscount, Amount(order.NegotiatedDiscount), Amount(terms.NegotiatedDiscount)));
        }

        if (terms.Warranty?.Id != order.WarrantyPackageId)
        {
            changes.Add(new(SaleChange.Warranty, order.WarrantyName, terms.Warranty?.Name));
        }

        if (terms.TradeIn?.AppointmentId != order.TradeInAppointmentId)
        {
            changes.Add(new(SaleChange.TradeIn, order.TradeInLabel, terms.TradeIn?.Label));
        }

        var allowance = terms.TradeIn?.Allowance ?? 0m;
        if (allowance != order.TradeInAllowance)
        {
            changes.Add(new(SaleChange.TradeInAllowance, Amount(order.TradeInAllowance), Amount(allowance)));
        }

        return changes;
    }

    private static string Amount(decimal value) =>
        value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

}
