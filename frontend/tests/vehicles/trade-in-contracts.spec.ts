import { expect, test } from "@playwright/test";
import { emptyValuation } from "@/lib/intake/vehicle-draft";
import { parseTradeInQuote, tradeInRequest } from "@/lib/vehicles/trade-in";

test("trade-in requests capture exact vehicle inputs without registration or a client price", () => {
  const draft = {
    make: "Proton",
    model: "Saga",
    trim: "1.3 Premium AT",
    manufactureYear: "2020",
    mileageKilometres: "90000",
  };
  expect(tradeInRequest(draft)).toEqual({
    ...draft,
    manufactureYear: 2020,
    mileageKilometres: 90000,
  });
  expect(tradeInRequest(emptyValuation)).toBeNull();
  for (const value of ["", "-1", "1.5", "1e3", "Infinity", "99999999999999999999"]) {
    expect(tradeInRequest({ ...draft, manufactureYear: value })).toBeNull();
    expect(tradeInRequest({ ...draft, mileageKilometres: value })).toBeNull();
  }
});

test("only ordered exact MYR quote bounds can become an indicative allowance", () => {
  const quote = { minValuation: 9000, maxValuation: 10000, currency: "MYR", valuationYear: 2026 };
  expect(parseTradeInQuote({ ...quote, privateField: "discarded" })).toEqual(quote);
  for (const invalid of [
    { currency: "USD" },
    { minValuation: -1 },
    { maxValuation: 8999.99 },
    { minValuation: 9000.001 },
    { maxValuation: 10000.001 },
    { minValuation: Infinity },
    { maxValuation: Number.MAX_VALUE },
    { valuationYear: 2026.5 },
  ]) {
    expect(parseTradeInQuote({ ...quote, ...invalid })).toBeNull();
  }
});
