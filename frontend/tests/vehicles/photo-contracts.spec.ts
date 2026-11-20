import { expect, test } from "@playwright/test";
import { readBoundedBytes } from "@/lib/http";
import { PHOTO_UPLOAD_MAX_BYTES } from "@/lib/intake/contracts";
import {
  parseListingPhotos,
  parseListingPhotoReceipt,
  matchesListingUpload,
  moveListingPhoto,
} from "@/lib/vehicles/listing-photos";
import { listingDraft, listingDraftContents } from "@/lib/vehicles/listing-draft";

const ids = [
  "47cf2705-95cc-48b6-b907-64bf18dd87da",
  "2f35e1b2-602b-43dc-8c5f-4085893b0ea8",
  "e26136c6-6e1b-46c2-9bb5-c28e2f01dd8c",
];
const photo = {
  id: ids[0],
  contentType: "image/jpeg",
  bytes: 1234,
  width: 960,
  height: 640,
  createdAt: "2026-10-27T00:00:00+00:00",
  availableAt: "2026-10-27T00:00:01+00:00",
};

test("photo request bodies accept 4 MiB and reject one additional byte", async () => {
  const maximumBytes = 4 * 1024 * 1024;
  const signal = new AbortController().signal;
  const accepted = new Response(new Uint8Array(maximumBytes));
  const rejected = new Response(new Uint8Array(maximumBytes + 1));

  const bytes = await readBoundedBytes(accepted.body, signal, PHOTO_UPLOAD_MAX_BYTES);
  expect(bytes.byteLength).toBe(maximumBytes);
  await expect(
    readBoundedBytes(rejected.body, signal, PHOTO_UPLOAD_MAX_BYTES),
  ).rejects.toMatchObject({ status: 413 });
});

test("uploaded photo pages validate bounded metadata and omit private fields", () => {
  const page = { items: [{ ...photo, actorId: ids[1] }], total: 12, page: 2, pageSize: 10 };
  expect(parseListingPhotos(page)).toEqual({ ...page, items: [photo] });
  expect(parseListingPhotos({ ...page, items: [{ ...photo, availableAt: null }] })).not.toBeNull();
  for (const changed of [
    { id: "invalid" },
    { bytes: 0 },
    { bytes: 10 * 1024 * 1024 + 1 },
    { width: 1.5 },
    { height: -1 },
    { contentType: "image/svg+xml" },
    { contentType: ["image/jpeg"] },
    { createdAt: "2026-10-27T00:00:00" },
    { availableAt: "invalid" },
  ]) {
    expect(parseListingPhotos({ ...page, items: [{ ...photo, ...changed }] })).toBeNull();
  }
  expect(parseListingPhotos({ ...page, pageSize: 8 })).toBeNull();
  expect(parseListingPhotos({ ...page, items: [photo, photo] })).toBeNull();
});

test("photo order changes retain off-page identities and save through the same draft", () => {
  const draft = {
    ...listingDraft(null),
    vehiclePrice: "45000.01",
    roadTax: "90.00",
    transmission: "Automatic",
    fuel: "Petrol",
    description: "Maintained vehicle",
    photoIds: [...ids],
  };
  const moved = moveListingPhoto(draft.photoIds, ids[2], -1);
  expect(moved).toEqual([ids[0], ids[2], ids[1]]);
  expect(draft.photoIds).toEqual(ids);
  expect(moveListingPhoto(moved, ids[0], 1)).toEqual([ids[2], ids[0], ids[1]]);
  expect(moveListingPhoto(ids, ids[0], -1)).toEqual(ids);
  expect(moveListingPhoto(ids, ids[2], 1)).toEqual(ids);
  expect(moveListingPhoto(ids, "unavailable", 1)).toEqual(ids);
  expect(listingDraftContents({ ...draft, photoIds: moved })).toMatchObject({
    photoIds: moved,
    vehiclePrice: 45000.01,
  });
  expect(listingDraftContents({ ...draft, photoIds: [] })?.photoIds).toEqual([]);
});

test("upload receipts require the vehicle identity and validated photo metadata", () => {
  const receipt = { ...photo, vehicleId: ids[1] };
  expect(parseListingPhotoReceipt({ ...receipt, privateStorageKey: "private" })).toEqual(receipt);
  expect(parseListingPhotoReceipt({ ...receipt, availableAt: null })).toEqual({
    ...receipt,
    availableAt: null,
  });
  for (const changed of [
    { vehicleId: undefined },
    { vehicleId: "invalid" },
    { id: undefined },
    { bytes: 0 },
    { contentType: ["image/jpeg"] },
    { availableAt: "invalid" },
  ]) {
    expect(parseListingPhotoReceipt({ ...receipt, ...changed })).toBeNull();
  }
});

test("upload confirmation binds the captured ID, vehicle, MIME and exact file size", () => {
  const receipt = { ...photo, vehicleId: ids[1] };
  const upload = { id: ids[0], file: { type: "image/jpeg", size: photo.bytes } };
  expect(matchesListingUpload(receipt, ids[1], upload)).toBe(true);
  for (const changed of [
    { id: ids[2] },
    { vehicleId: ids[2] },
    { contentType: "image/png" },
    { bytes: photo.bytes + 1 },
  ]) {
    expect(matchesListingUpload({ ...receipt, ...changed }, ids[1], upload)).toBe(false);
  }
});
