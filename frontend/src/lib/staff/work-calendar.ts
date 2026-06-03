import { parseItems, parsePage, type Page } from "@/lib/api";
import { parseActor, type CurrentActor } from "@/lib/auth/actor";
import { isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";
import { calendarDate, calendarRange, isCalendarDate } from "@/lib/calendar";
import { DEFAULT_PAGE_SIZE, isPageSize } from "@/lib/pagination";
import type { SearchParams } from "@/lib/query";

export interface WorkCalendarScope {
  branchId: string;
  employee: string;
}

export interface WorkCalendarFilters extends WorkCalendarScope {
  date: string;
  page: number;
  pageSize: number;
}

export interface WorkItem {
  id: string;
  kind: "Intake" | "TestDrive" | "Workshop" | "Sale";
  branchId: string;
  branchName: string;
  registrationPlate: string;
  vehicle: string;
  status: string;
  handlerId: string | null;
  handlerEmail: string | null;
  inspectionId?: string | null;
}

export interface ScheduledWork extends WorkItem {
  kind: "Intake" | "TestDrive";
  startsAt: string;
  endsAt: string;
}

interface WorkScopeSnapshot {
  actor: CurrentActor;
  branchId: string;
  employee: string;
}

export interface WorkCalendarSnapshot extends WorkScopeSnapshot {
  items: ScheduledWork[];
  total: number;
  from: string;
  to: string;
}

export type UnscheduledWorkPage = Page<WorkItem> & WorkScopeSnapshot;

export function hasWorkCalendarAccess(role: string) {
  return ["SalesAgent", "WorkshopTechnician", "BranchManager", "GeneralManager"].includes(role);
}

export function workCalendarParams(parameters: SearchParams): WorkCalendarFilters {
  const page = Number(parameters.page ?? 1);
  const size = Number(parameters.pageSize ?? DEFAULT_PAGE_SIZE);
  return {
    date: isCalendarDate(parameters.date) ? parameters.date : calendarDate(),
    branchId: isUuid(parameters.branchId) ? parameters.branchId : "",
    employee:
      typeof parameters.employee === "string" ? parameters.employee.trim().slice(0, 160) : "",
    page: Number.isSafeInteger(page) && page > 0 && page <= 2147483647 ? page : 1,
    pageSize: isPageSize(size) ? size : DEFAULT_PAGE_SIZE,
  };
}

export function workCalendarQuery(filters: WorkCalendarFilters, unscheduled = false) {
  const query = new URLSearchParams();
  if (filters.branchId) {
    query.set("branchId", filters.branchId);
  }
  if (filters.employee) {
    query.set("employee", filters.employee.trim());
  }
  if (unscheduled) {
    query.set("page", String(filters.page));
    query.set("pageSize", String(filters.pageSize));
  } else {
    const range = calendarRange(filters.date);
    query.set("from", range.from);
    query.set("to", range.to);
  }
  return query;
}

function parseScope(value: unknown): WorkScopeSnapshot | null {
  if (!isRecord(value) || !isUuid(value.branchId) || typeof value.employee !== "string") {
    return null;
  }
  const actor = parseActor(value.actor);
  if (!actor || !hasWorkCalendarAccess(actor.role) || value.employee.length > 160) {
    return null;
  }
  return { actor, branchId: value.branchId, employee: value.employee };
}

function parseWorkItem(value: unknown): WorkItem | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    !isUuid(value.branchId) ||
    typeof value.branchName !== "string" ||
    typeof value.registrationPlate !== "string" ||
    typeof value.vehicle !== "string" ||
    typeof value.status !== "string" ||
    (value.handlerId !== null && !isUuid(value.handlerId)) ||
    (value.handlerEmail !== null && typeof value.handlerEmail !== "string") ||
    (value.handlerId === null) !== (value.handlerEmail === null)
  ) {
    return null;
  }
  const kind = (["Intake", "TestDrive", "Workshop", "Sale"] as const).find(
    (item) => item === value.kind,
  );
  if (!kind) {
    return null;
  }
  return {
    id: value.id,
    kind,
    branchId: value.branchId,
    branchName: value.branchName,
    registrationPlate: value.registrationPlate,
    vehicle: value.vehicle,
    status: value.status,
    handlerId: value.handlerId,
    handlerEmail: value.handlerEmail,
  };
}

function parseScheduled(value: unknown): ScheduledWork | null {
  const item = parseWorkItem(value);
  if (
    !item ||
    !isRecord(value) ||
    (item.kind !== "Intake" && item.kind !== "TestDrive") ||
    !isUtcInstant(value.startsAt) ||
    !isUtcInstant(value.endsAt) ||
    Date.parse(value.endsAt) <= Date.parse(value.startsAt)
  ) {
    return null;
  }
  return { ...item, kind: item.kind, startsAt: value.startsAt, endsAt: value.endsAt };
}

export function parseWorkCalendar(value: unknown): WorkCalendarSnapshot | null {
  const scope = parseScope(value);
  if (!scope || !isRecord(value) || !isCalendarDate(value.from) || !isCalendarDate(value.to)) {
    return null;
  }
  const items = parseItems(value.items, parseScheduled);
  const days = (Date.parse(value.to) - Date.parse(value.from)) / 86400000 + 1;
  if (!items || value.total !== items.length || items.length > 1000 || days < 1 || days > 42) {
    return null;
  }
  const from = value.from;
  const to = value.to;
  if (
    !uniqueWork(items) ||
    items.some((item) => {
      const day = calendarDate(new Date(item.startsAt));
      return item.branchId !== scope.branchId || day < from || day > to;
    })
  ) {
    return null;
  }
  return { ...scope, items, total: items.length, from, to };
}

export function parseUnscheduledWork(value: unknown): UnscheduledWorkPage | null {
  const scope = parseScope(value);
  const page = parsePage(value, parseWorkItem);
  if (
    !scope ||
    !page ||
    !uniqueWork(page.items) ||
    page.items.some(
      (item) =>
        (item.kind !== "Workshop" && item.kind !== "Sale") ||
        !item.handlerId ||
        item.branchId !== scope.branchId,
    )
  ) {
    return null;
  }
  if (!isRecord(value) || !Array.isArray(value.items)) {
    return null;
  }
  const rawItems = value.items;
  const items = page.items.map((item, index) => {
    const raw: unknown = rawItems[index];
    if (
      !isRecord(raw) ||
      (item.kind === "Workshop" ? !isUuid(raw.inspectionId) : raw.inspectionId !== null)
    ) {
      return null;
    }
    return { ...item, inspectionId: raw.inspectionId as string | null };
  });
  if (items.some((item) => item === null)) {
    return null;
  }
  return { ...scope, ...page, items: items as WorkItem[] };
}

function uniqueWork(items: WorkItem[]) {
  return new Set(items.map((item) => `${item.kind}:${item.id}`)).size === items.length;
}
