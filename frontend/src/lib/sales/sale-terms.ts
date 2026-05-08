import { parseMyr } from "@/lib/currency-input";
import type { Sale, SaleOptions } from "./sale";

// The terms the handling agent keys, as typed: amounts stay text until they are saved.
export interface SaleTermsDraft {
  discount: string;
  warranty: string;
  tradeIn: string;
  allowance: string;
}

export function saleTermsDraft(sale: Sale["sale"]): SaleTermsDraft {
  return {
    discount: sale.negotiatedDiscount.toFixed(2),
    warranty: sale.warrantyPackageId ?? "",
    tradeIn: sale.tradeInAppointmentId ?? "",
    allowance: sale.tradeInAllowance > 0 ? sale.tradeInAllowance.toFixed(2) : "",
  };
}

// The draft read as amounts, checked against the chosen trade-in's valuation when the options are known.
export function readSaleTerms(draft: SaleTermsDraft, options: SaleOptions | null) {
  const negotiated = parseMyr(draft.discount.trim());
  const credit = draft.tradeIn ? parseMyr(draft.allowance.trim()) : 0;
  const chosen = options?.tradeIns.find((item) => item.appointmentId === draft.tradeIn);
  const overValuation = Boolean(chosen && (credit ?? 0) > chosen.maximum);
  const valid =
    negotiated !== null && credit !== null && (!draft.tradeIn || (credit > 0 && !overValuation));
  return { negotiated, credit, chosen, overValuation, valid };
}

export function saleTermsChanged(draft: SaleTermsDraft, sale: Sale["sale"]) {
  const { negotiated, credit } = readSaleTerms(draft, null);
  return (
    negotiated !== sale.negotiatedDiscount ||
    (draft.warranty || null) !== sale.warrantyPackageId ||
    (draft.tradeIn || null) !== sale.tradeInAppointmentId ||
    (credit ?? 0) !== sale.tradeInAllowance
  );
}
