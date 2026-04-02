import { parseMyr } from "@/lib/currency-input";
import { parseQuote, parseVehicle } from "@/lib/intake/parse";
import type { ValuationDraft } from "@/lib/intake/vehicle-draft";

export function tradeInRequest(draft: ValuationDraft) {
  if (!/^\d+$/.test(draft.manufactureYear) || !/^\d+$/.test(draft.mileageKilometres)) {
    return null;
  }
  return parseVehicle({
    make: draft.make,
    model: draft.model,
    trim: draft.trim,
    manufactureYear: Number(draft.manufactureYear),
    mileageKilometres: Number(draft.mileageKilometres),
  });
}

export function parseTradeInQuote(value: unknown) {
  const quote = parseQuote(value);
  if (
    !quote ||
    quote.currency !== "MYR" ||
    parseMyr(String(quote.minValuation)) === null ||
    parseMyr(String(quote.maxValuation)) === null
  ) {
    return null;
  }
  return quote;
}
