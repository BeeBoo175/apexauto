import { parsePage } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";

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

export function moveListingPhoto(ids: string[], id: string, offset: -1 | 1) {
  const from = ids.indexOf(id);
  const to = from + offset;
  if (from < 0 || to < 0 || to >= ids.length) {
    return ids;
  }
  const next = [...ids];
  [next[from], next[to]] = [next[to], next[from]];
  return next;
}
