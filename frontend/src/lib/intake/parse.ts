import { isPageSize } from "@/lib/pagination";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { angles } from "./contracts";
import type {
  Appointment,
  BookingReceipt,
  Branch,
  Page,
  PhotoReceipt,
  Quote,
  Slot,
  SlotOffer,
  Taxonomy,
  VehicleInput,
} from "./contracts";

const text = (value: unknown): value is string => typeof value === "string" && value.length > 0;
const integer = (value: unknown): value is number =>
  Number.isSafeInteger(value) && Number(value) >= 0;
const timestamp = (value: unknown): value is string =>
  text(value) && /(?:Z|\+00:00)$/.test(value) && Number.isFinite(Date.parse(value));
const date = (value: unknown): value is string =>
  text(value) &&
  /^\d{4}-\d{2}-\d{2}$/.test(value) &&
  Number.isFinite(Date.parse(value)) &&
  new Date(value).toISOString().slice(0, 10) === value;

export function parseTaxonomy(value: unknown): Taxonomy | null {
  if (!isRecord(value) || !text(value.currency) || !Array.isArray(value.makes)) {
    return null;
  }
  const makes = parseItems(value.makes, parseMake);
  return makes ? { currency: value.currency, makes } : null;
}

function parseTrim(value: unknown) {
  if (!isRecord(value) || !text(value.name) || typeof value.baselinePrice !== "number") {
    return null;
  }
  if (!Number.isFinite(value.baselinePrice) || value.baselinePrice < 0) {
    return null;
  }
  return { name: value.name, baselinePrice: value.baselinePrice };
}

function parseModel(value: unknown) {
  if (!isRecord(value) || !text(value.name)) {
    return null;
  }
  const trims = parseItems(value.trims, parseTrim);
  return trims ? { name: value.name, trims } : null;
}

function parseMake(value: unknown) {
  if (!isRecord(value) || !text(value.name)) {
    return null;
  }
  const models = parseItems(value.models, parseModel);
  return models ? { name: value.name, models } : null;
}

export function parseItems<T>(value: unknown, parse: (item: unknown) => T | null): T[] | null {
  if (!Array.isArray(value)) {
    return null;
  }
  const items: T[] = [];
  for (const item of value) {
    const parsed = parse(item);
    if (parsed === null) {
      return null;
    }
    items.push(parsed);
  }
  return items;
}

export function parseAngle(value: unknown) {
  return angles.find((angle, index) => angle === value || index === value) ?? null;
}

export function parseQuote(value: unknown): Quote | null {
  if (
    !isRecord(value) ||
    typeof value.minValuation !== "number" ||
    !Number.isFinite(value.minValuation) ||
    value.minValuation < 0 ||
    typeof value.maxValuation !== "number" ||
    !Number.isFinite(value.maxValuation) ||
    value.maxValuation < value.minValuation ||
    !text(value.currency) ||
    !integer(value.valuationYear)
  ) {
    return null;
  }
  return {
    minValuation: value.minValuation,
    maxValuation: value.maxValuation,
    currency: value.currency,
    valuationYear: value.valuationYear,
  };
}

export function parseVehicle(value: unknown): VehicleInput | null {
  if (
    !isRecord(value) ||
    !text(value.make) ||
    !text(value.model) ||
    !text(value.trim) ||
    !integer(value.manufactureYear) ||
    !integer(value.mileageKilometres)
  ) {
    return null;
  }
  return {
    make: value.make,
    model: value.model,
    trim: value.trim,
    manufactureYear: value.manufactureYear,
    mileageKilometres: value.mileageKilometres,
  };
}

export function parseBranch(value: unknown): Branch | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !text(value.name) ||
    !text(value.address) ||
    !text(value.city) ||
    !text(value.state) ||
    !text(value.phone)
  ) {
    return null;
  }
  return {
    id: value.id,
    name: value.name,
    address: value.address,
    city: value.city,
    state: value.state,
    phone: value.phone,
  };
}

export function parsePage<T>(
  value: unknown,
  parseItem: (item: unknown) => T | null,
): Page<T> | null {
  if (
    !isRecord(value) ||
    !Array.isArray(value.items) ||
    !integer(value.total) ||
    !integer(value.page) ||
    value.page < 1 ||
    !isPageSize(value.pageSize) ||
    value.items.length > value.pageSize
  ) {
    return null;
  }
  const items = parseItems(value.items, parseItem);
  if (!items || items.length > value.total) {
    return null;
  }
  return { items, total: value.total, page: value.page, pageSize: value.pageSize };
}

export function parseSlot(value: unknown): Slot | null {
  if (
    !isRecord(value) ||
    !timestamp(value.startsAt) ||
    !timestamp(value.endsAt) ||
    Date.parse(value.endsAt) <= Date.parse(value.startsAt)
  ) {
    return null;
  }
  return { startsAt: value.startsAt, endsAt: value.endsAt };
}

export function parseSlots(value: unknown): SlotOffer | null {
  if (
    !isRecord(value) ||
    !isUuid(value.branchId) ||
    !date(value.date) ||
    !isRecord(value.dateWindow) ||
    !date(value.dateWindow.firstDate) ||
    !date(value.dateWindow.lastDate) ||
    !Array.isArray(value.slots) ||
    value.slots.length > 1440
  ) {
    return null;
  }
  const slots = parseItems(value.slots, parseSlot);
  if (!slots) {
    return null;
  }
  return {
    branchId: value.branchId,
    date: value.date,
    dateWindow: { firstDate: value.dateWindow.firstDate, lastDate: value.dateWindow.lastDate },
    slots,
  };
}

export function parsePhoto(value: unknown): PhotoReceipt | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !timestamp(value.expiresAt) ||
    !text(value.contentType) ||
    !integer(value.bytes) ||
    !integer(value.width) ||
    !integer(value.height)
  ) {
    return null;
  }
  const angle = parseAngle(value.angle);
  const status = ["Available", "Pending", "DeletionPending", "Deleted"] as const;
  const state = status.find((item) => item === value.status);
  if (!angle || !state) {
    return null;
  }
  return {
    id: value.id,
    angle,
    status: state,
    expiresAt: value.expiresAt,
    contentType: value.contentType,
    bytes: value.bytes,
    width: value.width,
    height: value.height,
  };
}

export function parseBooking(value: unknown): BookingReceipt | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.branchId) ||
    !text(value.registrationPlate) ||
    !timestamp(value.createdAt) ||
    !parseSlot(value.slot) ||
    !parseVehicle(value.vehicle) ||
    !parseQuote(value.quote) ||
    !Array.isArray(value.photos) ||
    value.photos.length !== 4
  ) {
    return null;
  }
  const photos = parseItems(value.photos, parseBookingPhoto);
  const slot = parseSlot(value.slot);
  const vehicle = parseVehicle(value.vehicle);
  const quote = parseQuote(value.quote);
  if (!photos || !slot || !vehicle || !quote) {
    return null;
  }
  if (
    new Set(photos.map((photo) => photo.angle)).size !== 4 ||
    new Set(photos.map((photo) => photo.id)).size !== 4
  ) {
    return null;
  }
  return {
    id: value.id,
    branchId: value.branchId,
    registrationPlate: value.registrationPlate,
    createdAt: value.createdAt,
    slot,
    vehicle,
    quote,
    photos,
  };
}

function parseBookingPhoto(value: unknown) {
  if (!isRecord(value) || !isUuid(value.id)) {
    return null;
  }
  const angle = parseAngle(value.angle);
  return angle ? { id: value.id, angle } : null;
}

export function parseAppointment(value: unknown): Appointment | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.branchId) ||
    !text(value.branchName) ||
    !text(value.registrationPlate) ||
    !parseSlot(value) ||
    !timestamp(value.createdAt) ||
    (value.cancelledAt !== null && !timestamp(value.cancelledAt)) ||
    (value.noShowAt !== null && !timestamp(value.noShowAt)) ||
    (value.checkedInAt !== null && !timestamp(value.checkedInAt))
  ) {
    return null;
  }
  const slot = parseSlot(value);
  if (!slot) {
    return null;
  }
  return {
    id: value.id,
    branchId: value.branchId,
    branchName: value.branchName,
    registrationPlate: value.registrationPlate,
    createdAt: value.createdAt,
    cancelledAt: value.cancelledAt,
    noShowAt: value.noShowAt,
    checkedInAt: value.checkedInAt,
    ...slot,
  };
}

export function parseCancellation(value: unknown) {
  if (!isRecord(value) || !isUuid(value.appointmentId) || !timestamp(value.cancelledAt)) {
    return null;
  }
  return { appointmentId: value.appointmentId, cancelledAt: value.cancelledAt };
}
