import { parsePage } from "@/lib/api";
import { parseActor, type CurrentActor } from "@/lib/auth/actor";
import { isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";

export const testDriveEventTitles: Record<string, string> = {
  Booked: "Test drive booked",
  Claimed: "Claimed by staff",
  Assigned: "Assigned to staff",
  Released: "Released to the queue",
  Started: "Test drive started",
  Completed: "Test drive completed",
  NotCompleted: "Test drive not completed",
  CustomerCancelled: "Cancelled by the customer",
  StaffCancelled: "Cancelled by staff",
  VehicleReserved: "Cancelled: vehicle reserved",
  ListingWithdrawn: "Cancelled: listing withdrawn",
  InspectionReopened: "Cancelled: inspection reopened",
  InspectionRepairRequested: "Cancelled: repairs requested",
};

// One retained step of a booking: what happened, who did it and when. `handler` is who held it after the step.
export interface TestDriveEvent {
  id: string;
  kind: string;
  recordedAt: string;
  actor: string;
  actorId: string | null;
  handler: string | null;
  handlerId: string | null;
  reason: string | null;
  notes: string | null;
}

// Shaped for the shared staff history reader, which pages by `totalCount`.
export interface TestDriveHistory {
  actor: CurrentActor;
  bookingId: string;
  items: TestDriveEvent[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export const testDriveHistoryPath = (id: string) => `/api/staff/test-drives/${id}/history`;

const optionalText = (value: unknown): value is string | null =>
  value === null || (typeof value === "string" && value.trim() !== "");
const optionalId = (value: unknown): value is string | null => value === null || isUuid(value);

function parseEvent(value: unknown): TestDriveEvent | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    typeof value.kind !== "string" ||
    !Object.hasOwn(testDriveEventTitles, value.kind) ||
    !isUtcInstant(value.recordedAt) ||
    typeof value.actor !== "string" ||
    !value.actor.trim() ||
    !optionalId(value.actorId) ||
    !optionalText(value.handler) ||
    !optionalId(value.handlerId) ||
    !optionalText(value.reason) ||
    !optionalText(value.notes)
  ) {
    return null;
  }
  return value as unknown as TestDriveEvent;
}

export function parseStaffTestDriveHistory(value: unknown): TestDriveHistory | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  const page = parsePage(
    isRecord(value) ? { ...value, total: value.totalCount } : value,
    parseEvent,
  );
  if (
    !actor ||
    !isRecord(value) ||
    !isUuid(value.bookingId) ||
    !page ||
    new Set(page.items.map((item) => item.id)).size !== page.items.length
  ) {
    return null;
  }
  return {
    actor,
    bookingId: value.bookingId,
    items: page.items,
    totalCount: page.total,
    page: page.page,
    pageSize: page.pageSize,
  };
}

export const appendTestDriveHistory = (
  previous: TestDriveHistory,
  next: TestDriveHistory,
): TestDriveHistory => {
  const items = new Map([...previous.items, ...next.items].map((item) => [item.id, item]));
  return { ...next, items: [...items.values()] };
};

// The line under an event's heading, for what the heading alone does not say.
export function testDriveEventSummary(item: TestDriveEvent) {
  if (item.kind === "Assigned" && item.handler) {
    return `Now with ${item.handler}.`;
  }
  if (item.kind === "Claimed" && item.handler) {
    return `${item.handler} took it on.`;
  }
  return null;
}
