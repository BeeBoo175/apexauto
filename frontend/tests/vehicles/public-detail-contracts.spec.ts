import { expect, test } from "@playwright/test";
import { parsePublicDetail } from "@/lib/vehicles/public-detail";

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
