import { expect, test } from "@playwright/test";
import { parseAppointmentPrefill, sellerPrefillHref } from "@/lib/intake/appointment-prefill";

const branchId = "3f1c2a54-8b6e-4d1a-9c47-0a5e6b7d8c90";

test("a showroom and a bay in the address become a prefill with the Malaysian day", () => {
  const prefill = parseAppointmentPrefill(
    `?branchId=${branchId}&startsAt=2026-12-01T17%3A30%3A00Z`,
  );
  expect(prefill).toEqual({
    branchId,
    startsAt: "2026-12-01T17:30:00Z",
    date: "2026-12-02",
  });
});

test("a showroom alone preselects the showroom only", () => {
  expect(parseAppointmentPrefill(`?branchId=${branchId}`)).toEqual({
    branchId,
    startsAt: null,
    date: null,
  });
  expect(parseAppointmentPrefill(`?branchId=${branchId}&startsAt=tomorrow`)?.startsAt).toBeNull();
  expect(
    parseAppointmentPrefill(`?branchId=${branchId}&startsAt=2026-12-01T09:00:00%2B08:00`)?.startsAt,
  ).toBeNull();
});

test("an unknown or malformed showroom is ignored", () => {
  for (const search of ["", "?branchId=", "?branchId=klang", "?startsAt=2026-12-01T01:00:00Z"]) {
    expect(parseAppointmentPrefill(search)).toBeNull();
  }
});

test("the booking link round-trips through the parser", () => {
  const href = sellerPrefillHref(branchId, "2026-12-01T01:00:00Z");
  expect(parseAppointmentPrefill(href.slice(href.indexOf("?")))?.startsAt).toBe(
    "2026-12-01T01:00:00Z",
  );
});
