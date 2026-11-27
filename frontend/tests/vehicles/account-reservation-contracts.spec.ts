import { expect, test } from "@playwright/test";
import {
  accountReservationPage,
  accountReservationFilters,
  accountReservationQuery,
  parseAccountReservation,
  parseAccountReservations,
  purchaseStateLabel,
} from "@/lib/vehicles/account-reservations";
import {
  matchesCustomerCancellation,
  parseCustomerCancellation,
} from "@/lib/vehicles/customer-reservation-cancellation";
import { parseReservationConflict } from "@/lib/vehicles/reservation-errors";

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
  isHandled: false,
  dueAt: null,
  release: null,
  settledAt: null,
  deliveredAt: null,
};
const snapshot = {
  actor: { userId: owner, role: "Customer", branchId: null },
  items: [item],
  active: item,
  awaitingCollection: [],
  collectedTotal: 0,
  page: 1,
  pageSize: 5,
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
  const purchased = {
    ...item,
    isActive: false,
    release: null,
    settledAt: "2026-10-30T02:00:00Z",
  };
  expect(parseAccountReservation(purchased)?.release).toBeNull();
  expect(
    parseAccountReservations({ ...snapshot, items: [purchased], active: null })?.items[0].receipt
      .id,
  ).toBe(id);
  // A hold an agent handles stays active; a handled closed hold is a contradiction.
  expect(parseAccountReservation({ ...item, isHandled: true })?.isHandled).toBe(true);
  expect(parseAccountReservation({ ...released, isHandled: true })).toBeNull();
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
  expect(accountReservationPage({ page: "3" })).toBe(3);
  expect(accountReservationPage({ page: "2147483648" })).toBe(1);
});

test("only paid, uncollected cars are listed as awaiting collection and labelled apart from collected ones", () => {
  const waiting = { ...item, isActive: false, settledAt: "2026-10-30T02:00:00Z" };
  const collected = { ...waiting, deliveredAt: "2026-10-31T02:00:00Z" };
  const parse = (awaitingCollection: unknown) =>
    parseAccountReservations({ ...snapshot, awaitingCollection });

  // The list is independent of the history page, which here holds only the active hold.
  expect(parse([waiting])?.awaitingCollection).toHaveLength(1);
  expect(
    Object.fromEntries(
      new URLSearchParams(
        accountReservationQuery(1, { from: "", to: "", search: "" }, "collected"),
      ),
    ),
  ).toEqual({ page: "1", view: "collected" });
  for (const collectedTotal of [undefined, -1, 1.5]) {
    expect(parseAccountReservations({ ...snapshot, collectedTotal })).toBeNull();
  }
  for (const invalid of [undefined, [item], [collected], [{ ...waiting, settledAt: null }]]) {
    expect(parse(invalid)).toBeNull();
  }
  expect(purchaseStateLabel(parseAccountReservation(waiting)!)).toBe("Awaiting collection");
  expect(purchaseStateLabel(parseAccountReservation(collected)!)).toBe("Collected");
  expect(parseAccountReservation({ ...waiting, settledAt: null })).toBeNull();
  expect(parseAccountReservation({ ...collected, settledAt: null })).toBeNull();
});

test("a pending payment carries only the provider payment address", () => {
  const pendingPayment = {
    requestId: id,
    vehicleId: other,
    branchName: "Petaling Jaya",
    make: "Honda",
    model: "City",
    trim: "V",
    manufactureYear: 2022,
    expiresAt: "2026-10-30T01:15:00Z",
    bookedByStaff: false,
    paymentUrl: "https://www.billplz-sandbox.com/bills/abc123",
  };
  const parse = (change: object) =>
    parseAccountReservations({ ...snapshot, pendingPayment: { ...pendingPayment, ...change } });

  expect(parse({})?.pendingPayment?.paymentUrl).toBe(pendingPayment.paymentUrl);
  expect(parse({ paymentUrl: null })?.pendingPayment?.paymentUrl).toBeNull();
  expect(parse({ bookedByStaff: true })?.pendingPayment?.bookedByStaff).toBe(true);
  expect(parse({ bookedByStaff: undefined })).toBeNull();
  expect(parse({ bookedByStaff: "yes" })).toBeNull();
  expect(parse({ paymentUrl: "https://evil.example.com/bills/abc123" })).toBeNull();
  expect(parse({ paymentUrl: "http://www.billplz-sandbox.com/bills/abc123" })).toBeNull();
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

test("reservation filters read the plain section keys and ignore the retired prefixed keys and recognized conflicts use safe guidance", () => {
  const filters = accountReservationFilters({
    from: "2026-10-30",
    to: "2026-10-31",
    search: "  Petaling Jaya  ",
    reservationFrom: "2026-01-01",
  });
  expect(Object.fromEntries(new URLSearchParams(accountReservationQuery(2, filters)))).toEqual({
    page: "2",
    from: "2026-10-30",
    to: "2026-10-31",
    search: "Petaling Jaya",
  });
  expect(accountReservationFilters({ search: "x".repeat(161) }).search).toHaveLength(160);
  expect(parseReservationConflict("customer-has-reservation")?.message).toContain("sales team");
  expect(parseReservationConflict("vehicle-reserved")?.message).toContain("another customer");
  expect(parseReservationConflict("private backend error")).toBeUndefined();
  expect(parseReservationConflict({ code: "vehicle-reserved" })).toBeUndefined();
});
