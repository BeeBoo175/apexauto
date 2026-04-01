import { expect, test } from "@playwright/test";
import {
  parseReservationDetail,
  parseReservationDetailSnapshot,
} from "@/lib/vehicles/reservation-detail";

const ids = [
  "47cf2705-95cc-48b6-b907-64bf18dd87da",
  "2f35e1b2-602b-43dc-8c5f-4085893b0ea8",
  "e26136c6-6e1b-46c2-9bb5-c28e2f01dd8c",
];
const receipt = {
  id: ids[0],
  vehicleId: ids[1],
  branchId: ids[2],
  publicationId: ids[1],
  buyer: { legalName: "Nur Aina", email: "private@example.invalid", nric: "private" },
  price: {
    vehiclePrice: 60000,
    processingFee: 500,
    roadTax: 90,
    serviceTax: 40,
    promoDiscount: 1000,
    total: 59630,
  },
  promoCode: "Save1000",
  deposit: 500,
  createdAt: "2026-10-26T00:00:00Z",
  expiresAt: "2026-10-28T00:00:00Z",
};
const item = {
  receipt,
  branchName: "Glenmarie",
  make: "Proton",
  model: "Saga",
  trim: "Standard",
  manufactureYear: 2020,
  isActive: true,
  release: null,
};
const raw = { status: 0, item };
const release = {
  id: ids[2],
  reservationId: receipt.id,
  kind: "StaffCancelled",
  reason: "Customer requested release",
  recordedAt: "2026-10-26T01:00:00Z",
  refund: 500,
};

test("detail projection preserves exact retained prices and refunds while omitting buyer contact and nric", () => {
  const parsed = parseReservationDetail(raw)!;
  expect(parsed).toMatchObject({ reservation: { id: ids[0], buyer: "Nur Aina" }, deposit: 500 });
  expect(parsed.price).toEqual(receipt.price);
  expect(JSON.stringify(parsed)).not.toMatch(/private|email|nric/);
  expect(parseReservationDetailSnapshot(parsed)).toEqual(parsed);
  const refunded = parseReservationDetail({ ...raw, item: { ...item, isActive: false, release } })!;
  expect(refunded.release).toMatchObject({ id: release.id, reason: release.reason, refund: 500 });
  expect(parseReservationDetailSnapshot(refunded)).toEqual(refunded);
  for (const changed of [
    { status: 1 },
    { item: { ...item, isActive: false } },
    { item: { ...item, release } },
    { item: { ...item, receipt: { ...receipt, publicationId: "bad" } } },
    { item: { ...item, receipt: { ...receipt, deposit: 499 } } },
    { item: { ...item, receipt: { ...receipt, price: { ...receipt.price, total: 59631 } } } },
    { item: { ...item, receipt: { ...receipt, price: { ...receipt.price, roadTax: 90.001 } } } },
  ]) {
    expect(parseReservationDetail({ ...raw, ...changed })).toBeNull();
  }
  for (const changed of [
    { reservationId: ids[1] },
    { kind: "Unknown" },
    { reason: null },
    { refund: 499 },
    { recordedAt: "2026-10-25T00:00:00Z" },
  ]) {
    expect(
      parseReservationDetail({
        ...raw,
        item: { ...item, isActive: false, release: { ...release, ...changed } },
      }),
    ).toBeNull();
  }
  expect(parseReservationDetailSnapshot({ ...parsed, release })).toBeNull();
  expect(
    parseReservationDetailSnapshot({ ...refunded, release: { ...refunded.release, refund: 499 } }),
  ).toBeNull();
});
