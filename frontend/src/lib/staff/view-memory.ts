import { isRecord } from "@/lib/auth/contracts";
import { isCalendarDate } from "@/lib/calendar";
import { calendarHref, calendarParams } from "./calendar";
import { intakeQueueParams, staffParams, staffViewQuery } from "./query";

export type IntakeView = "calendar" | "list" | "queue";

export function intakeView(value: unknown, agent: boolean): IntakeView {
  if (value === "calendar") {
    return "calendar";
  }
  return agent ? "queue" : "list";
}

export interface ViewMemory {
  calendar?: string;
  list?: string;
  queue?: string;
}

export const viewMemoryKey = "apexauto:intake-views:v1";

export function parseViewMemory(value: string | null, actor: string): ViewMemory {
  if (!value || value.length > 16384) {
    return {};
  }
  try {
    const parsed: unknown = JSON.parse(value);
    if (!isRecord(parsed) || parsed.actor !== actor) {
      return {};
    }
    return {
      calendar: storedQuery(parsed.calendar),
      list: storedQuery(parsed.list),
      queue: storedQuery(parsed.queue),
    };
  } catch {
    return {};
  }
}

function storedQuery(value: unknown) {
  return typeof value === "string" && value.length <= 4096 ? value : undefined;
}

export function viewQuery(href: string) {
  return href.slice(href.indexOf("?") + 1);
}

export function restoreViewHref(view: IntakeView, href: string, remembered?: string) {
  if (!remembered) {
    return href;
  }
  const requested = Object.fromEntries(new URLSearchParams(viewQuery(href)));
  const saved = Object.fromEntries(new URLSearchParams(remembered));
  if (view === "queue") {
    const { filters, page } = intakeQueueParams(saved);
    if (!validSearch(filters.search)) {
      filters.search = "";
    }
    return `/dashboard/intake?${staffViewQuery(filters, page)}`;
  }
  if (view === "calendar") {
    const date = isCalendarDate(saved.date) ? saved.date : requested.date;
    return calendarHref(calendarParams({ ...requested, date }));
  }
  const shared = staffParams(requested).filters;
  const previous = staffParams(saved);
  const sharedKeys = ["from", "to", "status", "branchId"] as const;
  const sameScope = sharedKeys.every((key) => shared[key] === previous.filters[key]);
  const sorts = [
    "startsAt",
    "registrationPlate",
    "vehicle",
    "customerName",
    "handlerEmail",
    "branchName",
    "status",
  ];
  const filters = {
    ...previous.filters,
    queue: "" as const,
    from: shared.from,
    to: shared.to,
    status: shared.status,
    branchId: shared.branchId,
    search: validSearch(previous.filters.search) ? previous.filters.search : "",
    sort: sorts.includes(previous.filters.sort) ? previous.filters.sort : "startsAt",
  };
  return `/dashboard/intake?${staffViewQuery(filters, sameScope ? previous.page : 1)}`;
}

function validSearch(value: string) {
  return value.length <= 160 && !/[\u0000-\u001f\u007f-\u009f]/u.test(value);
}
