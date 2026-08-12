import { expect, test } from "@playwright/test";
import { parseTaxonomy } from "@/lib/intake/parse";
import { supportedManufactureYears } from "@/lib/intake/manufacture-years";

function taxonomy(trim: Record<string, unknown>) {
  return {
    currency: "MYR",
    maximumMileageKilometres: 500000,
    makes: [{ name: "Perodua", models: [{ name: "Myvi", trims: [trim] }] }],
  };
}

test("taxonomy keeps each trim's year range and the mileage ceiling", () => {
  const parsed = parseTaxonomy(
    taxonomy({ name: "1.5 AV", baselinePrice: 60000, firstYear: 2018, lastYear: null }),
  );

  expect(parsed?.maximumMileageKilometres).toBe(500000);
  expect(parsed?.makes[0].models[0].trims[0]).toEqual({
    name: "1.5 AV",
    baselinePrice: 60000,
    firstYear: 2018,
    lastYear: null,
  });
});

test("taxonomy without a year range or mileage ceiling is rejected", () => {
  expect(parseTaxonomy(taxonomy({ name: "1.5 AV", baselinePrice: 60000 }))).toBeNull();
  expect(
    parseTaxonomy({
      ...taxonomy({ name: "X", baselinePrice: 1, firstYear: 2018 }),
      maximumMileageKilometres: -1,
    }),
  ).toBeNull();
});

test("year choices run from the current Malaysian year back to the trim's first year", () => {
  const trim = { name: "1.5 AV", baselinePrice: 60000, firstYear: 2023, lastYear: null };
  const now = new Date("2026-06-15T04:00:00Z");

  expect(supportedManufactureYears(trim, now)).toEqual([2026, 2025, 2024, 2023]);
  expect(supportedManufactureYears({ ...trim, lastYear: 2024 }, now)).toEqual([2024, 2023]);
  expect(supportedManufactureYears(undefined, now)).toEqual([]);
});

test("the Malaysian new year starts before UTC does", () => {
  const trim = { name: "1.5 AV", baselinePrice: 60000, firstYear: 2025, lastYear: null };

  expect(supportedManufactureYears(trim, new Date("2025-12-31T17:00:00Z"))).toEqual([2026, 2025]);
});
