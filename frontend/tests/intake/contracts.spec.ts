import { expect, test } from "@playwright/test";
import { parseActor, actorDestination } from "../../src/lib/auth/actor";
import { parseSlots, parseTaxonomy } from "../../src/lib/intake/parse";

const id = "d9a6e19a-c149-4fd7-bc85-ce7b2ff3f091";

test("actor destinations require a validated persisted role and branch shape", () => {
  expect(parseActor({ userId: id, role: "SalesAgent", branchId: null })).toBeNull();
  expect(parseActor({ userId: id, role: "Administrator", branchId: id })).toBeNull();
  expect(parseActor({ userId: id, role: "unknown", branchId: null })).toBeNull();
  expect(
    actorDestination({ userId: id, role: "Customer", branchId: null }, "/dashboard/intake"),
  ).toBe("/account");
  expect(actorDestination({ userId: id, role: "SalesAgent", branchId: id }, "/sell")).toBe(
    "/dashboard/intake",
  );
  expect(actorDestination({ userId: id, role: "Administrator", branchId: null })).toBe(
    "/dashboard",
  );
});

test("external intake snapshots reject partial taxonomy and impossible calendar dates", () => {
  expect(parseTaxonomy({ currency: "MYR", makes: [{ name: "Proton", models: [{}] }] })).toBeNull();
  expect(
    parseSlots({
      branchId: id,
      date: "2026-02-31",
      dateWindow: { firstDate: "2026-02-01", lastDate: "2026-03-01" },
      slots: [],
    }),
  ).toBeNull();
});
