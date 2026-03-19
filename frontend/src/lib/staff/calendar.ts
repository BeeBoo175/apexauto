import type { SearchParams } from "@/lib/query";
import { isRecord } from "@/lib/auth/contracts";
import { calendarDate, calendarRange, isCalendarDate } from "@/lib/calendar";
import { parseWorklistItem } from "./parse";
import type { WorklistItem } from "./contracts";

export interface CalendarFilters {
  date: string;
  status: string;
  branchId: string;
  range?: { from: string; to: string };
}

export interface CalendarSnapshot {
  items: WorklistItem[];
  total: number;
  from: string;
  to: string;
}

export function calendarParams(params: SearchParams): CalendarFilters {
  const range =
    isCalendarDate(params.from) &&
    isCalendarDate(params.to) &&
    params.from <= params.to &&
    Date.parse(params.to) - Date.parse(params.from) < 366 * 86400000
      ? { from: params.from, to: params.to }
      : undefined;
  const requested = isCalendarDate(params.date) ? params.date : (range?.from ?? calendarDate());
  const date = range && requested < range.from ? range.from : requested;
  return {
    date: range && date > range.to ? range.to : date,
    status: typeof params.status === "string" ? params.status : "All",
    branchId: typeof params.branchId === "string" ? params.branchId : "",
    range,
  };
}

export function calendarQuery(filters: CalendarFilters) {
  const range = calendarReadRange(filters);
  const query = new URLSearchParams({ ...range, status: filters.status });
  if (filters.branchId) {
    query.set("branchId", filters.branchId);
  }
  return query.toString();
}

export function calendarReadRange(filters: CalendarFilters) {
  const visible = calendarRange(filters.date);
  if (!filters.range) {
    return visible;
  }
  return {
    from: filters.range.from > visible.from ? filters.range.from : visible.from,
    to: filters.range.to < visible.to ? filters.range.to : visible.to,
  };
}

export function calendarHref(filters: CalendarFilters) {
  const query = new URLSearchParams({
    view: "calendar",
    date: filters.date,
  });
  if (filters.status !== "All") {
    query.set("status", filters.status);
  }
  if (filters.branchId) {
    query.set("branchId", filters.branchId);
  }
  if (filters.range) {
    query.set("from", filters.range.from);
    query.set("to", filters.range.to);
  }
  return `/dashboard/intake?${query}`;
}

export function parseCalendar(
  value: unknown,
  expected?: { from: string; to: string },
): CalendarSnapshot | null {
  if (
    !isRecord(value) ||
    !Array.isArray(value.items) ||
    !isCalendarDate(value.from) ||
    !isCalendarDate(value.to) ||
    !Number.isSafeInteger(value.total) ||
    value.total !== value.items.length ||
    value.items.length > 1000
  ) {
    return null;
  }
  if (expected && (value.from !== expected.from || value.to !== expected.to)) {
    return null;
  }
  const days = (Date.parse(value.to) - Date.parse(value.from)) / 86400000 + 1;
  const items = value.items.map(parseWorklistItem);
  if (
    days < 1 ||
    days > 42 ||
    items.some((item) => !item) ||
    new Set(items.map((item) => item?.id)).size !== items.length
  ) {
    return null;
  }
  const confirmedItems = items as WorklistItem[];
  const from = value.from;
  const to = value.to;
  if (
    confirmedItems.some((item) => {
      const day = calendarDate(new Date(item.startsAt));
      return day < from || day > to;
    })
  ) {
    return null;
  }
  return {
    items: confirmedItems,
    total: value.total as number,
    from: value.from,
    to: value.to,
  };
}
