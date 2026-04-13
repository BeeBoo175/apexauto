import { parsePage } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";

export const MAX_LISTING_PHOTOS = 4;

export interface ListingPhotoItem {
  id: string;
  contentType: string;
  bytes: number;
  width: number;
  height: number;
  createdAt: string;
  availableAt: string | null;
}

const positiveInteger = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) > 0;
const utcDate = (value: unknown): value is string =>
  typeof value === "string" && /(?:Z|\+00:00)$/.test(value) && Number.isFinite(Date.parse(value));

function parsePhoto(value: unknown): ListingPhotoItem | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    typeof value.contentType !== "string" ||
    !["image/jpeg", "image/png", "image/webp"].includes(value.contentType) ||
    !positiveInteger(value.bytes) ||
    value.bytes > 10 * 1024 * 1024 ||
    !positiveInteger(value.width) ||
    !positiveInteger(value.height) ||
    !utcDate(value.createdAt) ||
    (value.availableAt !== null && !utcDate(value.availableAt))
  ) {
    return null;
  }
  return {
    id: value.id,
    contentType: value.contentType,
    bytes: value.bytes,
    width: value.width,
    height: value.height,
    createdAt: value.createdAt,
    availableAt: value.availableAt as string | null,
  };
}

export function parseListingPhotos(value: unknown) {
  const page = parsePage(value, parsePhoto);
  if (!page || new Set(page.items.map((item) => item.id)).size !== page.items.length) {
    return null;
  }
  return page;
}

export function parseListingPhotoReceipt(value: unknown) {
  const photo = parsePhoto(value);
  if (!photo || !isRecord(value) || !isUuid(value.vehicleId)) {
    return null;
  }
  return { ...photo, vehicleId: value.vehicleId };
}

export function matchesListingUpload(
  receipt: NonNullable<ReturnType<typeof parseListingPhotoReceipt>>,
  vehicleId: string,
  upload: { id: string; file: { type: string; size: number } },
) {
  return (
    receipt.id === upload.id &&
    receipt.vehicleId === vehicleId &&
    receipt.contentType === upload.file.type &&
    receipt.bytes === upload.file.size
  );
}

export function moveListingPhoto(ids: string[], id: string, offset: -1 | 1) {
  const from = ids.indexOf(id);
  const to = from + offset;
  return reorderListingPhoto(ids, id, to);
}

export function reorderListingPhoto(ids: string[], id: string, position: number) {
  const from = ids.indexOf(id);
  if (
    from < 0 || !Number.isInteger(position) || position < 0 ||
    position >= ids.length || from === position
  ) {
    return ids;
  }
  const next = [...ids];
  next.splice(from, 1);
  next.splice(position, 0, id);
  return next;
}

export interface ListingPhotoSlot {
  left: number;
  right: number;
  top: number;
  bottom: number;
}

export function listingPhotoDropIndex(slots: ListingPhotoSlot[], x: number, y: number) {
  let index = -1;
  let distance = Infinity;
  slots.forEach((slot, position) => {
    const next =
      (x - (slot.left + slot.right) / 2) ** 2 +
      (y - (slot.top + slot.bottom) / 2) ** 2;
    if (next < distance) {
      index = position;
      distance = next;
    }
  });
  return index;
}
