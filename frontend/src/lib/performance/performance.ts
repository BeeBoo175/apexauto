import { parseActor, type CurrentActor } from "@/lib/auth/actor";
import { isNonNegativeInteger, isRecord, isUuid } from "@/lib/auth/contracts";

// Manager operations reporting. Sections are read independently, so each has its own contract and parser.

export const sectionNames = [
  "headline",
  "pipeline",
  "stock-flow",
  "stage-waiting",
  "workload",
  "branches",
] as const;
export type SectionName = (typeof sectionNames)[number];

export const hasPerformanceAccess = (actor: Pick<CurrentActor, "role">) =>
  actor.role === "BranchManager" || actor.role === "GeneralManager";

export interface PerformancePeriod {
  from: string;
  to: string;
  branchId: string;
}

export interface PeriodTotals {
  vehiclesDelivered: number;
  deliveredValue: number;
  listedCount: number;
  checkInToListedDays: number | null;
  customerNoShows: number;
}

export interface PerformanceHeadline {
  actor: CurrentActor;
  current: PeriodTotals;
  previous: PeriodTotals;
}

export interface PipelineGroup {
  group: string;
  count: number;
  agreedTotal: number;
}

export interface PerformancePipeline {
  actor: CurrentActor;
  groups: PipelineGroup[];
}

export interface StockWeek {
  weekStart: string;
  checkIns: number;
  deliveries: number;
  // The agreed total of the week's deliveries, the same measure as the headline's delivered value.
  deliveredValue: number;
}

export interface PerformanceStockFlow {
  actor: CurrentActor;
  weeks: StockWeek[];
}

export interface StageRow {
  stage: string;
  inStageNow: number;
  completedCount: number;
  averageDays: number | null;
}

export interface PerformanceStageWaiting {
  actor: CurrentActor;
  stages: StageRow[];
}

export interface WorkloadPerson {
  id: string;
  name: string;
  role: string;
  branchId: string;
  branchName: string;
  pictureId: string | null;
  under2Days: number;
  twoToSevenDays: number;
  overSevenDays: number;
  total: number;
}

// One page of the people holding open work. The counts describe the whole scope, whatever page is shown.
export interface PerformanceWorkload {
  actor: CurrentActor;
  people: WorkloadPerson[];
  total: number;
  salesCount: number;
  workshopCount: number;
  stretchedCount: number;
  page: number;
  pageSize: number;
}

export interface BranchRow {
  branchId: string;
  branchName: string;
  current: PeriodTotals;
  previous: PeriodTotals;
  weeks: StockWeek[];
}

export interface PerformanceBranches {
  actor: CurrentActor;
  branches: BranchRow[];
}

export const pipelineLabels: Record<string, string> = {
  Unclaimed: "Unclaimed",
  Handling: "Handling",
  NeedsApproval: "Needs approval",
  Approved: "Approved",
  SettledUndelivered: "Settled, not delivered",
};

export const stageLabels: Record<string, string> = {
  AwaitingInspection: "Awaiting inspection",
  InInspection: "In inspection",
  InRepair: "In repair",
  AwaitingListing: "Awaiting listing",
  Listed: "Listed",
  SaleInProgress: "Sale in progress",
};

const isNumber = (value: unknown): value is number =>
  typeof value === "number" && Number.isFinite(value);

const isOptionalNumber = (value: unknown): value is number | null =>
  value === null || isNumber(value);

function parseList<T>(value: unknown, parseItem: (item: unknown) => T | null): T[] | null {
  if (!Array.isArray(value)) {
    return null;
  }
  const items: T[] = [];
  for (const entry of value) {
    const item = parseItem(entry);
    if (!item) {
      return null;
    }
    items.push(item);
  }
  return items;
}

function parseTotals(value: unknown): PeriodTotals | null {
  if (
    !isRecord(value) ||
    !isNonNegativeInteger(value.vehiclesDelivered) ||
    !isNumber(value.deliveredValue) ||
    !isNonNegativeInteger(value.listedCount) ||
    !isOptionalNumber(value.checkInToListedDays) ||
    !isNonNegativeInteger(value.customerNoShows)
  ) {
    return null;
  }
  return value as unknown as PeriodTotals;
}

export function parseHeadline(value: unknown): PerformanceHeadline | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  const current = isRecord(value) ? parseTotals(value.current) : null;
  const previous = isRecord(value) ? parseTotals(value.previous) : null;
  return actor && current && previous ? { actor, current, previous } : null;
}

function parsePipelineGroup(value: unknown): PipelineGroup | null {
  if (
    !isRecord(value) ||
    typeof value.group !== "string" ||
    !Object.hasOwn(pipelineLabels, value.group) ||
    !isNonNegativeInteger(value.count) ||
    !isNumber(value.agreedTotal)
  ) {
    return null;
  }
  return value as unknown as PipelineGroup;
}

export function parsePipeline(value: unknown): PerformancePipeline | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  const groups = isRecord(value) ? parseList(value.groups, parsePipelineGroup) : null;
  return actor && groups ? { actor, groups } : null;
}

function parseStockWeek(value: unknown): StockWeek | null {
  if (
    !isRecord(value) ||
    typeof value.weekStart !== "string" ||
    !/^\d{4}-\d{2}-\d{2}$/.test(value.weekStart) ||
    !isNonNegativeInteger(value.checkIns) ||
    !isNonNegativeInteger(value.deliveries) ||
    !isNumber(value.deliveredValue) ||
    value.deliveredValue < 0
  ) {
    return null;
  }
  return value as unknown as StockWeek;
}

export function parseStockFlow(value: unknown): PerformanceStockFlow | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  const weeks = isRecord(value) ? parseList(value.weeks, parseStockWeek) : null;
  return actor && weeks ? { actor, weeks } : null;
}

function parseStage(value: unknown): StageRow | null {
  if (
    !isRecord(value) ||
    typeof value.stage !== "string" ||
    !Object.hasOwn(stageLabels, value.stage) ||
    !isNonNegativeInteger(value.inStageNow) ||
    !isNonNegativeInteger(value.completedCount) ||
    !isOptionalNumber(value.averageDays)
  ) {
    return null;
  }
  return value as unknown as StageRow;
}

export function parseStageWaiting(value: unknown): PerformanceStageWaiting | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  const stages = isRecord(value) ? parseList(value.stages, parseStage) : null;
  return actor && stages ? { actor, stages } : null;
}

function parseWorkloadPerson(value: unknown): WorkloadPerson | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    typeof value.name !== "string" ||
    typeof value.role !== "string" ||
    !isUuid(value.branchId) ||
    typeof value.branchName !== "string" ||
    !(value.pictureId === null || isUuid(value.pictureId)) ||
    !isNonNegativeInteger(value.under2Days) ||
    !isNonNegativeInteger(value.twoToSevenDays) ||
    !isNonNegativeInteger(value.overSevenDays) ||
    !isNonNegativeInteger(value.total)
  ) {
    return null;
  }
  return value as unknown as WorkloadPerson;
}

export function parseWorkload(value: unknown): PerformanceWorkload | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  const people = isRecord(value) ? parseList(value.people, parseWorkloadPerson) : null;
  if (
    !actor ||
    !people ||
    !isRecord(value) ||
    !isNonNegativeInteger(value.total) ||
    !isNonNegativeInteger(value.salesCount) ||
    !isNonNegativeInteger(value.workshopCount) ||
    !isNonNegativeInteger(value.stretchedCount) ||
    !isNonNegativeInteger(value.page) ||
    !isNonNegativeInteger(value.pageSize)
  ) {
    return null;
  }
  return {
    actor,
    people,
    total: value.total,
    salesCount: value.salesCount,
    workshopCount: value.workshopCount,
    stretchedCount: value.stretchedCount,
    page: value.page,
    pageSize: value.pageSize,
  };
}

function parseBranchRow(value: unknown): BranchRow | null {
  const current = isRecord(value) ? parseTotals(value.current) : null;
  const previous = isRecord(value) ? parseTotals(value.previous) : null;
  const weeks = isRecord(value) ? parseList(value.weeks, parseStockWeek) : null;
  if (
    !isRecord(value) ||
    !isUuid(value.branchId) ||
    typeof value.branchName !== "string" ||
    !current ||
    !previous ||
    !weeks
  ) {
    return null;
  }
  return { branchId: value.branchId, branchName: value.branchName, current, previous, weeks };
}

export function parseBranches(value: unknown): PerformanceBranches | null {
  const actor = isRecord(value) ? parseActor(value.actor) : null;
  const branches = isRecord(value) ? parseList(value.branches, parseBranchRow) : null;
  return actor && branches ? { actor, branches } : null;
}

export const sectionParsers = {
  headline: parseHeadline,
  pipeline: parsePipeline,
  "stock-flow": parseStockFlow,
  "stage-waiting": parseStageWaiting,
  workload: parseWorkload,
  branches: parseBranches,
} as const;
