import { expect, test } from "@playwright/test";
import {
  accountReservationPage,
  parseAccountReservations,
} from "@/lib/vehicles/account-reservations";
import {
  matchesCustomerCancellation,
  parseCustomerCancellation,
} from "@/lib/vehicles/customer-reservation-cancellation";

const owner = "47cf2705-95cc-48b6-b907-64bf18dd87da";
const id = "2f35e1b2-602b-43dc-8c5f-4085893b0ea8";
const other = "e26136c6-6e1b-46c2-9bb5-c28e2f01dd8c";
const receipt = {
  id,
  number: "VRS-2026-000001",
  vehicleId: other,
  branchId: other,
  publicationId: other,
  buyer: {
    legalName: "Alex Tan",
    email: "alex@example.invalid",
    nric: "private",
    mobile: "private",
  },
  promoId: null,
  promoCode: null,
  deposit: 500,
  price: {
    vehiclePrice: 60000,
    processingFee: 500,
    roadTax: 90,
    serviceTax: 40,
    promoDiscount: 0,
    total: 60630,
  },
  createdAt: "2026-10-30T00:00:00Z",
  expiresAt: "2026-11-01T00:00:00Z",
};
const item = {
  receipt,
  branchName: "Petaling Jaya",
  make: "Honda",
  model: "City",
  trim: "V",
  manufactureYear: 2022,
  coverPhotoId: other,
  isActive: true,
  release: null,
};
const snapshot = {
  actor: { userId: owner, role: "Customer", branchId: null },
  items: [item],
  active: item,
  page: 1,
  pageSize: 25,
  total: 1,
};
const cancellation = {
  id: other,
  reservationId: id,
  actorId: owner,
  kind: "CustomerCancelled",
  reason: null,
  recordedAt: "2026-10-30T01:00:00Z",
  refund: 500,
};

test("customer history accepts no registration and keeps an independent active hold while stripping private buyer fields", () => {
  const parsed = parseAccountReservations(snapshot)!;
  expect(parsed).not.toBeNull();
  expect(JSON.stringify(parsed)).not.toMatch(/nric|mobile|private|registrationPlate/);
  expect(parseAccountReservations(parsed)).toEqual(parsed);
  expect(parseAccountReservations({ ...snapshot, page: 2, items: [] })?.active?.receipt.id).toBe(
    id,
  );
  const released = { ...item, isActive: false, release: cancellation };
  expect(
    parseAccountReservations({ ...snapshot, items: [released], active: null })?.items[0].release
      ?.refund,
  ).toBe(500);
  for (const change of [
    { actor: { ...snapshot.actor, role: "GeneralManager" } },
    { pageSize: 50 },
    { active: null },
    { active: released },
    { items: [item, item] },
    { total: 2 },
    { items: [{ ...item, receipt: { ...receipt, price: { ...receipt.price, total: 1 } } }] },
    {
      items: [{ ...item, isActive: false, release: { ...cancellation, reservationId: other } }],
      active: null,
    },
    { items: [{ ...released, release: { ...cancellation, refund: 0 } }], active: null },
    {
      items: [{ ...released, release: { ...cancellation, recordedAt: "2026-10-29T00:00:00Z" } }],
      active: null,
    },
  ]) {
    expect(parseAccountReservations({ ...snapshot, ...change })).toBeNull();
  }
  expect(accountReservationPage({ page: "3", reservationPage: "2" })).toBe(2);
  expect(accountReservationPage({ reservationPage: "2147483648" })).toBe(1);
});

test("customer cancellation requires the exact operation, target and actor without a staff reason", () => {
  const parsed = parseCustomerCancellation(cancellation)!;
  const body = { operationId: other, reason: null };
  expect(matchesCustomerCancellation(parsed, body, id, owner)).toBe(true);
  expect(matchesCustomerCancellation(parsed, { ...body, operationId: owner }, id, owner)).toBe(
    false,
  );
  expect(matchesCustomerCancellation(parsed, body, other, owner)).toBe(false);
  expect(matchesCustomerCancellation(parsed, body, id, other)).toBe(false);
  for (const change of [
    { kind: "StaffCancelled" },
    { actorId: null },
    { reason: "Staff-only reason" },
    { refund: 499 },
  ]) {
    expect(parseCustomerCancellation({ ...cancellation, ...change })).toBeNull();
  }
});
