using System.Globalization;
using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static Dealership.Infrastructure.PdfDocumentLayout;

namespace Dealership.Infrastructure.Sales;

public sealed class SaleDocumentRenderer : ISaleDocumentRenderer
{
    public byte[] Render(SaleIssuedDocument document)
    {
        var title = document.Kind switch
        {
            nameof(SaleDocumentKind.PurchaseOrder) => "Vehicle Sales Agreement / Purchase Order",
            nameof(SaleDocumentKind.PaymentReceipt) => "Official Payment Receipt",
            nameof(SaleDocumentKind.HandoverNote) => "Vehicle Handover Note",
            _ => throw new ArgumentException("Unsupported issued sale document.", nameof(document))
        };
        return RenderPage(page =>
        {
            Configure(page, document.Number);
            page.Header().Element(container => Masthead(container, title, document.Number));
            page.Content().PaddingHorizontal(PageMargin, Unit.Millimetre).PaddingTop(SectionGap).Column(column =>
            {
                column.Spacing(Block);
                column.Item().Element(container => Identity(container, document));
                column.Item().Element(container => Vehicle(container, document.Contents.Review.Vehicle));
                if (document.Kind == nameof(SaleDocumentKind.HandoverNote))
                {
                    column.Item().Element(container => Handover(container, document.Contents.Sale));
                }
                else
                {
                    column.Item().Element(container => Pricing(container, document.Contents.Sale));
                    column.Item().Element(container => Payment(container, document.Contents.Sale));
                }

                column.Item().Element(container => Retention(container, document));
                if (document.Kind == nameof(SaleDocumentKind.PurchaseOrder) && document.ReplacementDocumentId is null)
                {
                    column.Item().Element(Signatures);
                }
            });
        });
    }

    private static void Identity(IContainer container, SaleIssuedDocument document)
    {
        var contents = document.Contents;
        container.Column(column =>
        {
            column.Spacing(Related);
            if (document.ReplacementDocumentId is not null)
            {
                column.Item().Text($"VOID - replaced by {document.Correction!.ReplacementNumber}").Bold().FontColor(Signal);
            }

            column.Item().Text(contents.BranchName).FontSize(Title).Bold();
            column.Item().Text(contents.BranchAddress).FontColor(Muted);
            column.Item().Row(row =>
            {
                Fact(row, "Issued (Malaysia time)", Moment(document.IssuedAt), span: 2);
                Fact(row, "Reservation", contents.ReservationNumber);
                Fact(row, "Original purchase order", contents.Sale.OrderNumber!);
            });
            column.Item().Element(item => SectionTitle(item, "Buyer"));
            if (contents.Review.BuyerDetailsRemoved)
            {
                column.Item().Text("The buyer's personal details were removed after the retention period.")
                    .FontColor(Muted);
                return;
            }

            column.Item().Row(row =>
            {
                Fact(row, "Name", contents.Review.BuyerName!, span: 3, bold: true);
                Fact(row, "NRIC", contents.Review.BuyerNric!);
            });
            column.Item().Text(contents.Review.BuyerAddress!);
        });
    }

    private static void Vehicle(IContainer container, SaleReviewedVehicle vehicle)
    {
        container.Column(column =>
        {
            column.Spacing(Related);
            column.Item().Element(item => SectionTitle(item, "Vehicle at settlement"));
            column.Item().Text($"{vehicle.ManufactureYear} {vehicle.Make} {vehicle.Model} {vehicle.Trim}")
                .FontSize(Strong).SemiBold();
            column.Item().Row(row =>
            {
                Fact(row, "Registration", vehicle.RegistrationPlate, bold: true);
                Fact(row, "VIN", vehicle.Vin, span: 2);
                Fact(row, "Recorded mileage", $"{vehicle.MileageKilometres.ToString("N0", CultureInfo.InvariantCulture)} km");
            });
        });
    }

    private static void Pricing(IContainer container, SaleSnapshot sale)
    {
        container.Column(column =>
        {
            column.Item().Element(item => SectionTitle(item, "Agreed commercial terms"));
            column.Item().PaddingTop(Related).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(120);
                });
                Price(table, "Vehicle price", sale.BasePrice);
                Price(table, "Processing fee", sale.ProcessingFee);
                Price(table, "Road tax", sale.RoadTax);
                Price(table, $"SST on processing fee ({(sale.SstRate * 100m).ToString("0.##", CultureInfo.InvariantCulture)}%)", sale.ServiceTax);
                Price(table, $"Warranty: {sale.WarrantyName ?? "None"}", sale.WarrantyPrice);
                Price(table, $"Promo discount{(sale.PromoCode is null ? "" : $" ({sale.PromoCode})")}", -sale.PromoDiscount);
                Price(table, "Negotiated discount", -sale.NegotiatedDiscount);
                Price(table, "Deposit credit", -sale.DepositCredit);
                Price(table, $"Trade-in credit: {sale.TradeInLabel ?? "None"}", -sale.TradeInAllowance);
                Price(table, "Balance payable at settlement", sale.BalancePayable, total: true);
            });
        });
    }

    private static void Price(TableDescriptor table, string label, decimal amount, bool total = false)
    {
        var left = table.Cell().BorderTop(total ? Heavy : 0).BorderColor(Graphite).PaddingVertical(Tight);
        var right = table.Cell().BorderTop(total ? Heavy : 0).BorderColor(Graphite).PaddingVertical(Tight);
        left.Text(label).FontSize(total ? Strong : Body);
        right.AlignRight().Text(Money(amount)).FontSize(total ? Strong : Body).SemiBold();
    }

    private static void Payment(IContainer container, SaleSnapshot sale)
    {
        var payment = sale.PaymentEvidence ?? throw new InvalidOperationException("Issued documents require settlement evidence.");
        container.Column(column =>
        {
            column.Spacing(Related);
            column.Item().Element(item => SectionTitle(item, "Settlement acknowledgment"));
            column.Item().Text($"Confirmed {Moment(sale.SettledAt!.Value)} (Malaysia time)");
            column.Item().Text("Deposit and trade-in credits are already included above.").FontSize(Small).FontColor(Muted);
            if (payment.Method == SalePaymentMethod.ZeroBalance)
            {
                column.Item().Text("Zero balance explicitly confirmed. No additional payment required.");
                return;
            }

            column.Item().Row(row =>
            {
                Fact(row, "Method", PaymentMethod(payment.Method), span: 2);
                Fact(row, "Received customer payment", Money(payment.ReceivedAmount), span: 2, emphasis: true);
            });
            if (payment.Method == SalePaymentMethod.HirePurchase)
            {
                column.Item().Row(row =>
                {
                    Fact(row, "Financing bank", payment.Bank!, span: 2);
                    Fact(row, "Financing amount", Money(payment.FinancingAmount), span: 2);
                });
                column.Item().Text($"Verified Letter of Undertaking: {payment.LouReference}");
                return;
            }

            column.Item().Text($"Payment reference: {payment.PaymentReference}");
            column.Item().Text($"Payment date: {payment.PaymentDate!.Value.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}");
        });
    }

    private static string PaymentMethod(SalePaymentMethod method) => method switch
    {
        SalePaymentMethod.Cash => "Cash",
        SalePaymentMethod.BankTransfer => "Bank transfer",
        SalePaymentMethod.HirePurchase => "Hire purchase",
        _ => throw new InvalidOperationException("Unsupported payment method.")
    };

    private static void Handover(IContainer container, SaleSnapshot sale)
    {
        var evidence = sale.HandoverEvidence ?? throw new InvalidOperationException("Handover notes require delivery evidence.");
        container.ShowEntire().Column(column =>
        {
            column.Spacing(Related);
            column.Item().Element(item => SectionTitle(item, "Completed handover"));
            column.Item().Text($"Settlement confirmed {Moment(sale.SettledAt!.Value)} (Malaysia time)");
            column.Item().Text($"Delivered {Moment(sale.DeliveredAt!.Value)} (Malaysia time)");
            column.Item().Row(row =>
            {
                Fact(row, "Final odometer", $"{evidence.OdometerKilometres.ToString("N0", CultureInfo.InvariantCulture)} km", span: 2);
                Fact(row, "Keys delivered", evidence.KeysDelivered.ToString(CultureInfo.InvariantCulture), span: 2);
            });
            column.Item().Text($"Customer receipt acknowledged: {Confirmed(evidence.CustomerAcknowledged)}");
            column.Item().Text($"Ownership transfer completed: {Confirmed(evidence.OwnershipTransferCompleted)}");
            column.Item().Text($"Insurance confirmed: {Confirmed(evidence.InsuranceConfirmed)}");
            column.Item().Text($"Recorded by handling agent: {sale.AgentId}").FontSize(Small).FontColor(Muted);
        });
    }

    private static string Confirmed(bool value) => value ? "Yes" : "No";

    private static void Retention(IContainer container, SaleIssuedDocument document)
    {
        container.Column(column =>
        {
            column.Spacing(Related);
            var removed = document.Contents.Review.BuyerDetailsRemoved;
            column.Item().Text(removed
                    ? "The buyer's personal details were removed after the retention period; the commercial record is retained."
                    : "This document retains the details recorded when issued.")
                .FontSize(Small).FontColor(Muted);
            if (document.ReplacementDocumentId is not null)
            {
                var reason = document.Correction!.Reason is { } text ? $": {text}" : ".";
                column.Item().Text($"Correction recorded {Moment(document.Correction.RecordedAt)}{reason}");
            }

            if (document.ReplacesDocumentId is { } originalId)
            {
                column.Item().Text($"Reissued document. Replaces document {originalId}.").FontSize(Small).FontColor(Muted);
            }
        });
    }

    private static void Signatures(IContainer container)
    {
        container.ShowEntire().PaddingTop(Block).Row(row =>
        {
            row.RelativeItem().PaddingRight(Block).BorderTop(Hairline).BorderColor(Line).PaddingTop(Related)
                .Text("Buyer signature / date").FontColor(Muted);
            row.RelativeItem().BorderTop(Hairline).BorderColor(Line).PaddingTop(Related)
                .Text("Authorized dealership signature / date").FontColor(Muted);
        });
    }
}
