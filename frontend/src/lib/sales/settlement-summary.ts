import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDate } from "@/lib/date-format";
import type { PaymentMethod } from "./completion-evidence";

const methodLabels: Record<PaymentMethod, string> = {
  Cash: "Cash",
  BankTransfer: "Bank transfer",
  HirePurchase: "Hire purchase",
  ZeroBalance: "No further payment",
};

// What the agent reads back before confirming: the entries as typed, since confirmation cannot be undone.
export function settlementSummary(
  buyerName: string,
  vehicle: {
    manufactureYear: number;
    make: string;
    model: string;
    trim: string;
    registrationPlate: string;
  },
  balance: number,
  payment: {
    method: PaymentMethod;
    received: string;
    reference: string;
    date: string;
    bank: string;
    financing: string;
    lou: string;
  },
) {
  const facts = [
    { label: "Buyer", value: buyerName },
    {
      label: "Car",
      value: `${vehicle.manufactureYear} ${vehicle.make} ${vehicle.model} ${vehicle.trim}`,
    },
    { label: "Registration", value: vehicle.registrationPlate },
    { label: "Balance", value: formatCurrency(balance) },
    { label: "Method", value: methodLabels[payment.method] },
  ];
  if (payment.method === "Cash" || payment.method === "BankTransfer") {
    facts.push(
      { label: "Received", value: formatCurrency(Number(payment.received)) },
      { label: "Reference", value: payment.reference },
      { label: "Payment date", value: payment.date && formatMalaysianDate(payment.date) },
    );
  }
  if (payment.method === "HirePurchase") {
    facts.push(
      { label: "Bank", value: payment.bank },
      { label: "Financed", value: formatCurrency(Number(payment.financing)) },
      { label: "From customer", value: formatCurrency(Number(payment.received)) },
      { label: "LOU reference", value: payment.lou },
    );
  }
  return facts;
}
