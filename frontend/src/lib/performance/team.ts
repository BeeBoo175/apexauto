import { parsePage } from "@/lib/api";
import { parseActor, type CurrentActor } from "@/lib/auth/actor";
import { isNonNegativeInteger, isRecord, isUuid, isUtcInstant } from "@/lib/auth/contracts";
import type { Page } from "@/lib/api";

export type TeamTab = "sales" | "workshop";

export const personStatusLabels: Record<string, string> = {
  Active: "Active",
  Transferred: "Transferred",
  Deactivated: "Deactivated",
};

export interface TeamPerson {
  personId: string;
  name: string;
  role: string;
  branchId: string;
  branchName: string;
  pictureId: string | null;
  status: string;
}

export interface SalesRow extends TeamPerson {
  checkIns: number;
  noShowCorrections: number;
  drivesCompleted: number;
  drivesNotCompleted: number;
  salesSettled: number;
  salesDelivered: number;
  assignmentToDeliveryDays: number | null;
  intakeHandled: number | null;
  testDrivesHeld: number | null;
  salesHandled: number | null;
}

export interface WorkshopRow extends TeamPerson {
  inspectionsSigned: number;
  reinspections: number;
  repairsCompleted: number;
  assignmentToSignOffDays: number | null;
  assignmentToReinspectionDays: number | null;
  openDrafts: number | null;
  openRepairs: number | null;
}

// One person's volume of work and average days for the whole team, whatever page is read.
export interface TeamPoint {
  personId: string;
  branchId: string;
  name: string;
  volume: number;
  days: number | null;
}

// The page fields stay at the top level, so the relay and the browser parse the same shape.
export type TeamPage<Row> = Page<Row> & { actor: CurrentActor; points: TeamPoint[] };

function parsePoint(value: unknown): TeamPoint | null {
  if (
    !isRecord(value) ||
    !isUuid(value.personId) ||
    !isUuid(value.branchId) ||
    typeof value.name !== "string" ||
    !isNonNegativeInteger(value.volume) ||
    !isOptionalDays(value.days)
  ) {
    return null;
  }
  return value as unknown as TeamPoint;
}

const isOptionalCount = (value: unknown) => value === null || isNonNegativeInteger(value);

const isOptionalDays = (value: unknown) =>
  value === null || (typeof value === "number" && Number.isFinite(value) && value >= 0);

function parsePerson(value: unknown): value is Record<string, unknown> & TeamPerson {
  return (
    isRecord(value) &&
    isUuid(value.personId) &&
    typeof value.name === "string" &&
    typeof value.role === "string" &&
    isUuid(value.branchId) &&
    typeof value.branchName === "string" &&
    (value.pictureId === null || isUuid(value.pictureId)) &&
    typeof value.status === "string" &&
    Object.hasOwn(personStatusLabels, value.status)
  );
}

function parseSalesRow(value: unknown): SalesRow | null {
  if (
    !parsePerson(value) ||
    !isNonNegativeInteger(value.checkIns) ||
    !isNonNegativeInteger(value.noShowCorrections) ||
    !isNonNegativeInteger(value.drivesCompleted) ||
    !isNonNegativeInteger(value.drivesNotCompleted) ||
    !isNonNegativeInteger(value.salesSettled) ||
    !isNonNegativeInteger(value.salesDelivered) ||
    !isOptionalDays(value.assignmentToDeliveryDays) ||
    !isOptionalCount(value.intakeHandled) ||
    !isOptionalCount(value.testDrivesHeld) ||
    !isOptionalCount(value.salesHandled)
  ) {
    return null;
  }
  return value as unknown as SalesRow;
}

function parseWorkshopRow(value: unknown): WorkshopRow | null {
  if (
    !parsePerson(value) ||
    !isNonNegativeInteger(value.inspectionsSigned) ||
    !isNonNegativeInteger(value.reinspections) ||
    !isNonNegativeInteger(value.repairsCompleted) ||
    !isOptionalDays(value.assignmentToSignOffDays) ||
    !isOptionalDays(value.assignmentToReinspectionDays) ||
    !isOptionalCount(value.openDrafts) ||
    !isOptionalCount(value.openRepairs)
  ) {
    return null;
  }
  return value as unknown as WorkshopRow;
}

function parseTeam<Row extends TeamPerson>(
  value: unknown,
  parseRow: (item: unknown) => Row | null,
): TeamPage<Row> | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  const page = parsePage(value, parseRow);
  const keys = page?.items.map((row) => `${row.personId}:${row.branchId}`) ?? [];
  const rawPoints = isRecord(value) && Array.isArray(value.points) ? value.points : null;
  const points = rawPoints?.map(parsePoint) ?? null;
  if (!actor || !page || !points || points.includes(null) || new Set(keys).size !== keys.length) {
    return null;
  }
  return { actor, ...page, points: points as TeamPoint[] };
}

export const parseSalesTeam = (value: unknown) => parseTeam(value, parseSalesRow);
export const parseWorkshopTeam = (value: unknown) => parseTeam(value, parseWorkshopRow);

export const milestoneLabels: Record<string, string> = {
  CheckIn: "Check-in",
  NoShowCorrection: "No-show correction",
  DriveCompleted: "Test drive completed",
  DriveNotCompleted: "Test drive not completed",
  SaleSettled: "Sale settled",
  SaleDelivered: "Sale delivered",
  InspectionSigned: "Inspection signed",
  Reinspection: "Re-inspection signed",
  RepairCompleted: "Repair completed",
};

export interface Milestone {
  kind: string;
  at: string;
  branchId: string;
  branchName: string;
  reference: string;
  recordKind: string;
  recordId: string;
  agreedTotal: number | null;
  hasAccess: boolean;
}

type MilestonePage = Page<Milestone> & {
  actor: CurrentActor;
  personId: string;
  personName: string;
};

function parseMilestone(value: unknown): Milestone | null {
  if (
    !isRecord(value) ||
    typeof value.kind !== "string" ||
    !Object.hasOwn(milestoneLabels, value.kind) ||
    !isUtcInstant(value.at) ||
    !isUuid(value.branchId) ||
    typeof value.branchName !== "string" ||
    typeof value.reference !== "string" ||
    typeof value.recordKind !== "string" ||
    !isUuid(value.recordId) ||
    !(value.agreedTotal === null || typeof value.agreedTotal === "number") ||
    typeof value.hasAccess !== "boolean"
  ) {
    return null;
  }
  return value as unknown as Milestone;
}

export function parseMilestonePage(value: unknown): MilestonePage | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  const page = parsePage(value, parseMilestone);
  if (
    !actor ||
    !page ||
    !isRecord(value) ||
    !isUuid(value.personId) ||
    typeof value.personName !== "string"
  ) {
    return null;
  }
  return { actor, personId: value.personId, personName: value.personName, ...page };
}

// The shared progressive history counts its entries as totalCount and grows by appending older pages.
export type MilestoneHistory = MilestonePage & { totalCount: number };

export function parseMilestoneHistory(value: unknown): MilestoneHistory | null {
  const page = parseMilestonePage(value);
  return page ? { ...page, totalCount: page.total } : null;
}

export function appendMilestones(
  previous: MilestoneHistory,
  next: MilestoneHistory,
): MilestoneHistory {
  return { ...next, items: [...previous.items, ...next.items] };
}

// Where a milestone's record opens, when the viewer has access to it today.
export function milestoneHref(milestone: Milestone) {
  switch (milestone.recordKind) {
    case "Appointment":
      return `/dashboard/intake/${milestone.recordId}`;
    case "TestDrive":
      return `/dashboard/test-drives?booking=${milestone.recordId}`;
    case "Sale":
      return `/dashboard/sales?sale=${milestone.recordId}`;
    default:
      return `/dashboard/inspection/${milestone.recordId}`;
  }
}
