import { expect, test } from "@playwright/test";
import {
  matchesPurchaseTerms,
  parsePurchasePrice,
  parsePurchaseQuote,
  promoCode,
} from "@/lib/vehicles/purchase-quote";

const price = {
  vehiclePrice: 45000,
  processingFee: 500,
  roadTax: 90,
  serviceTax: 40,
  promoDiscount: 1000,
  total: 44630,
};
const quote = { promoCode: "SAVE1000", promoId: "47cf2705-95cc-48b6-b907-64bf18dd87da", price };

test("promo preserves trimmed exact case and binds the response code and complete itemized money", () => {
  expect(promoCode("  Save1000  ")).toBe("Save1000");
  for (const code of ["", " ", "a".repeat(51), "SAVE\0"]) {
    expect(promoCode(code)).toBeNull();
  }
  expect(parsePurchaseQuote({ ...quote, privateField: "discard" }, "SAVE1000")).toEqual(quote);
  expect(parsePurchaseQuote(quote, "save1000")).toBeNull();
  expect(parsePurchaseQuote({ ...quote, promoId: null }, "SAVE1000")).toBeNull();
  for (const changed of [
    { total: 44631 },
    { promoDiscount: 45001, total: 629 },
    { vehiclePrice: 0 },
    { promoDiscount: -1 },
    { total: 44630.001 },
    { total: Number.POSITIVE_INFINITY },
  ]) {
    expect(parsePurchasePrice({ ...price, ...changed })).toBeNull();
  }
});

test("a promo quote must match every confirmed base price term before replacing the estimate", () => {
  const base = { ...price, promoDiscount: 0, total: 45630 };
  expect(matchesPurchaseTerms(quote, base)).toBe(true);
  for (const field of ["vehiclePrice", "processingFee", "roadTax", "serviceTax"] as const) {
    expect(matchesPurchaseTerms(quote, { ...base, [field]: base[field] + 1 })).toBe(false);
  }
});
