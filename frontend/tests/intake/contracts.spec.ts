import { expect, test } from "@playwright/test";
import { parseActor, actorDestination } from "../../src/lib/auth/actor";
import { parseBooking, parseSlots, parseTaxonomy } from "../../src/lib/intake/parse";

import { bookingMatchesRequest } from "../../src/lib/intake/receipt";

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

test("booking receipts bind the exact operation and four distinct photo angles", () => {
  const vehicle = {
    make: "Proton",
    model: "Saga",
    trim: "Standard",
    manufactureYear: 2020,
    mileageKilometres: 90000,
  };
  const quote = { minValuation: 10000, maxValuation: 12000, currency: "MYR", valuationYear: 2026 };
  const photos = ["Driver", "Passenger", "Front", "Rear"].map((angle) => ({
    id: crypto.randomUUID(),
    angle,
  }));
  const receipt = {
    id,
    branchId: id,
    registrationPlate: "VCA9701",
    slot: { startsAt: "2026-10-20T01:00:00Z", endsAt: "2026-10-20T02:30:00Z" },
    createdAt: "2026-10-19T01:00:00Z",
    vehicle,
    quote,
    photos,
  };
  const request = {
    id,
    branchId: id,
    registrationPlate: "VCA 9701",
    startsAt: receipt.slot.startsAt,
    vehicle,
    acceptedQuote: quote,
    photoIds: photos.map((photo) => photo.id),
  };
  expect(bookingMatchesRequest(receipt, request)).toBe(true);
  expect(bookingMatchesRequest(receipt, { ...request, id: crypto.randomUUID() })).toBe(false);
  expect(
    bookingMatchesRequest(receipt, { ...request, photoIds: photos.map(() => crypto.randomUUID()) }),
  ).toBe(false);
  expect(
    parseBooking({ ...receipt, photos: photos.map((photo) => ({ ...photo, angle: "Driver" })) }),
  ).toBeNull();
});
