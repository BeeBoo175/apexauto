import { expect, test } from "@playwright/test";
import { parsePublicDetail } from "@/lib/vehicles/public-detail";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { parsePurchasePrice } from "@/lib/vehicles/purchase-quote";
import { vehicleDetailSectionHref } from "@/lib/vehicles/query";
import { safeReturnTo } from "@/lib/auth/contracts";
import {
  matchesCustomerReservation,
  parseCustomerReservation,
  parseCustomerReservationRequest,
} from "@/lib/vehicles/customer-reservation";

const id = "47cf2705-95cc-48b6-b907-64bf18dd87da";
const photoId = "2f35e1b2-602b-43dc-8c5f-4085893b0ea8";
const listing = {
  id,
  branchId: id,
  branchName: "Glenmarie",
  make: "Proton",
  model: "Saga",
  trim: "Standard",
  manufactureYear: 2020,
  mileageKilometres: 90000,
  vehiclePrice: 45000,
  transmission: 0,
  fuel: 0,
  coverPhotoId: photoId,
  photoCount: 1,
  publishedAt: "2026-10-26T00:00:00+00:00",
  vin: "private-vehicle",
};
const point = {
  code: "FC-001",
  category: 0,
  description: "Frame condition",
  kind: null,
  severity: null,
  estimatedRepairCost: 0,
  notes: "private-note",
  photos: [id],
};
const price = {
  vehiclePrice: 45000,
  processingFee: 500,
  roadTax: 90,
  serviceTax: 40,
  promoDiscount: 0,
  total: 45630,
};
const raw = {
  publicationId: photoId,
  listing,
  roadTax: 90,
  description: "Approved showroom description",
  photoIds: [photoId],
  sstRate: 0.08,
  price,
  checklist: {
    catalogVersion: "apex-inspection-1",
    grade: 0,
    signedAt: listing.publishedAt,
    points: [point],
    technicianId: id,
  },
  buyer: "private-customer",
};

test("public detail retains itemized approved terms while discarding private report and customer fields", () => {
  const parsed = parsePublicDetail(raw, id)!;
  expect(parsed.price).toEqual(price);
  expect(parsed.checklist.grade).toBe("A");
  expect(parsed.checklist.points[0].category).toBe("FrameChassis");
  expect(JSON.stringify(parsed)).not.toMatch(/private|buyer|technicianId|vin|notes|photos/);
  expect(parsePublicDetail(parsed, id)).toEqual(parsed);
  expect(parsePublicDetail(raw, photoId)).toBeNull();
  expect(parsePublicDetail(raw, id.toUpperCase())).toEqual(parsed);
});

test("public detail rejects inconsistent price, photo identity and signed checklist projections", () => {
  for (const changed of [
    { publicationId: undefined },
    { publicationId: "invalid" },
    { price: { ...price, total: price.total + 1 } },
    { price: { ...price, vehiclePrice: price.vehiclePrice + 1, total: price.total + 1 } },
    { price: { ...price, roadTax: 91, total: price.total + 1 } },
    { price: { ...price, promoDiscount: 1 } },
    { price: { ...price, total: 45630.001 } },
    { sstRate: 2 },
    { photoIds: [id] },
    { photoIds: [photoId, photoId], listing: { ...listing, photoCount: 2 } },
    { checklist: { ...raw.checklist, signedAt: "yesterday" } },
    { checklist: { ...raw.checklist, points: [point, point] } },
    { checklist: { ...raw.checklist, points: [{ ...point, kind: "Finding" }] } },
    { checklist: { ...raw.checklist, points: [{ ...point, severity: "Advisory" }] } },
  ]) {
    expect(parsePublicDetail({ ...raw, ...changed }, id)).toBeNull();
  }
  const finding = { ...point, kind: "Finding", severity: "Advisory", estimatedRepairCost: 10 };
  expect(
    parsePublicDetail({ ...raw, checklist: { ...raw.checklist, points: [finding] } })?.checklist
      .points[0].severity,
  ).toBe("Advisory");
});

test("reservation confirmations bind exact operation, vehicle, publication, promo and buyer while omitting private identity fields", () => {
  const body = { operationId: id, publicationId: photoId, promoId: null };
  const receipt = {
    id,
    number: "VRS-2026-000001",
    vehicleId: photoId,
    branchId: id,
    publicationId: photoId,
    buyer: {
      legalName: "Nur Aina",
      email: "customer@example.invalid",
      nric: "private",
      mobile: "private",
    },
    promoId: null,
    promoCode: null,
    price,
    deposit: 500,
    createdAt: "2026-10-30T00:00:00Z",
    expiresAt: "2026-11-01T00:00:00Z",
  };
  const parsed = parseCustomerReservation(receipt)!;
  expect(parsed).not.toBeNull();
  expect(JSON.stringify(parsed)).not.toMatch(/private|nric|mobile/);
  expect(parseCustomerReservation(parsed)).toEqual(parsed);
  expect(matchesCustomerReservation(parsed, body, photoId, id, receipt.buyer.email)).toBe(true);
  for (const changed of [{ operationId: photoId }, { publicationId: id }, { promoId: id }]) {
    expect(
      matchesCustomerReservation(parsed, { ...body, ...changed }, photoId, id, receipt.buyer.email),
    ).toBe(false);
  }
  expect(matchesCustomerReservation(parsed, body, id, id, receipt.buyer.email)).toBe(false);
  expect(matchesCustomerReservation(parsed, body, photoId, photoId, receipt.buyer.email)).toBe(
    false,
  );
  expect(matchesCustomerReservation(parsed, body, photoId, id, "other@example.invalid")).toBe(
    false,
  );
  for (const changed of [
    { deposit: 0 },
    { expiresAt: "2026-11-01T00:00:01Z" },
    { createdAt: "2026-10-30T00:00:00" },
    { price: { ...price, total: price.total + 1 } },
    { promoId: undefined },
    { promoCode: "unexpected" },
    { buyer: { legalName: "", email: receipt.buyer.email } },
  ]) {
    expect(parseCustomerReservation({ ...receipt, ...changed })).toBeNull();
  }
  expect(parseCustomerReservationRequest({ ...body, customerId: photoId })).toEqual(body);
  expect(parseCustomerReservationRequest({ ...body, operationId: "invalid" })).toBeNull();
});

test("login returns accept implemented vehicle details and reject arbitrary routes or external destinations", () => {
  expect(safeReturnTo(`/vehicles/${id}`)).toBe(`/vehicles/${id}`);
  for (const destination of [
    "//example.invalid",
    "/vehicles/not-an-id",
    `/vehicles/${id}/reservations`,
    "/vehicles",
    "https://example.invalid",
  ]) {
    expect(safeReturnTo(destination)).toBe("/account");
  }
});

test("catalogue and purchase projections agree on exact MYR precision", () => {
  for (const amount of [
    0.01,
    123.45,
    45000.99,
    100.001,
    0.1 + 0.2,
    -1,
    NaN,
    Infinity,
    Number.MAX_SAFE_INTEGER,
    "100",
    null,
  ]) {
    const vehicle = { ...listing, vehiclePrice: amount };
    const purchase = {
      vehiclePrice: amount,
      processingFee: 0,
      roadTax: 0,
      serviceTax: 0,
      promoDiscount: 0,
      total: amount,
    };
    const accepted = amount === 0.01 || amount === 123.45 || amount === 45000.99;
    expect(Boolean(parseCatalogue({ items: [vehicle], total: 1, page: 1, pageSize: 12 }))).toBe(
      accepted,
    );
    expect(Boolean(parsePurchasePrice(purchase))).toBe(accepted);
    expect(
      Boolean(parsePublicDetail({ ...raw, listing: vehicle, price: purchase, roadTax: 0 })),
    ).toBe(accepted);
  }
});

test("legacy vehicle sections preserve repeated query values and reject unsupported destinations", () => {
  expect(
    vehicleDetailSectionHref(id, { section: "financing", ref: ["one", "two"], absent: undefined }),
  ).toBe(`/vehicles/${id}?ref=one&ref=two#financing`);
  expect(vehicleDetailSectionHref(id, { section: "trade-in" })).toBe(`/vehicles/${id}#trade-in`);
  expect(vehicleDetailSectionHref(id, {})).toBeNull();
  expect(vehicleDetailSectionHref(id, { section: "unsupported" })).toBeNull();
  expect(vehicleDetailSectionHref(id, { section: ["overview", "inspection"] })).toBeNull();
});
