import { expect, test } from "@playwright/test";
import {
  remainingHoldSeconds,
  saleDiscountOverLimit,
  saleHasHoldTimer,
} from "@/lib/sales/contracts";
import { tableParams, tableQuery, tableViewFilters } from "@/lib/sales/query";

test("table filters round-trip through the address and leave an unset view to the server", () => {
  const { filters, page } = tableParams({
    view: "History",
    outcome: "Expired",
    page: "2",
    sort: "x",
  });
  expect(filters.sort).toBe("");
  const query = new URLSearchParams(tableQuery(filters, page));
  expect([query.get("view"), query.get("outcome"), query.get("page")]).toEqual([
    "History",
    "Expired",
    "2",
  ]);
  expect(new URLSearchParams(tableQuery(tableParams({}).filters)).has("view")).toBe(false);
});

test("queue changes preserve the fixed columns' filters and sort while clearing the old outcome", () => {
  const { filters } = tableParams({
    view: "History",
    outcome: "Cancelled",
    reference: "VRS-2026",
    minimumHoursLeft: "6.5",
    maximumHoursLeft: "24",
    minimumDiscount: "100",
    maximumBalance: "70000",
    sort: "timeLeft",
    direction: "desc",
  });
  for (const view of [
    "Open",
    "BeingHandled",
    "NeedsApproval",
    "Approved",
    "AwaitingHandover",
    "Delivered",
    "History",
  ] as const) {
    const next = tableViewFilters(filters, view);
    expect(next).toEqual({ ...filters, view, outcome: "" });
    const parameters = Object.fromEntries(new URLSearchParams(tableQuery(next)));
    expect(tableParams(parameters).filters).toEqual(next);
  }
  expect(tableParams({ sort: "claimed", submittedFrom: "2026-10-31" }).filters.sort).toBe("");
  expect(tableQuery(tableParams({ submittedFrom: "2026-10-31" }).filters)).not.toContain(
    "submitted",
  );
});

test("removed Updated criteria do not survive old Sales table addresses", () => {
  const { filters } = tableParams({
    updatedFrom: "2026-10-31",
    updatedTo: "2026-11-01",
    sort: "updated",
  });
  expect(filters.sort).toBe("");
  const query = tableQuery(filters);
  expect(query).not.toContain("updated");
  expect(query).not.toContain("sort=");
});

test("hold time uses the stored paused duration and never counts below zero", () => {
  const now = Date.parse("2026-11-01T00:00:00Z");
  expect(remainingHoldSeconds({ dueAt: "2026-11-01T06:00:00Z", pausedSeconds: null }, now)).toBe(
    21600,
  );
  expect(remainingHoldSeconds({ dueAt: null, pausedSeconds: 43200 }, now)).toBe(43200);
  expect(remainingHoldSeconds({ dueAt: "2026-10-31T23:00:00Z", pausedSeconds: null }, now)).toBe(0);
  expect(saleDiscountOverLimit({ negotiatedDiscount: 500, discountLimitAtSubmit: 500 })).toBe(
    false,
  );
  expect(saleDiscountOverLimit({ negotiatedDiscount: 500.01, discountLimitAtSubmit: 500 })).toBe(
    true,
  );
  expect(saleDiscountOverLimit({ negotiatedDiscount: 900, discountLimitAtSubmit: null })).toBe(
    false,
  );
  for (const status of ["Open", "Handling", "PendingApproval", "Approved"] as const) {
    expect(saleHasHoldTimer(status)).toBe(true);
  }
  for (const status of ["Settled", "Delivered", "Expired", "Cancelled"] as const) {
    expect(saleHasHoldTimer(status)).toBe(false);
  }
});
