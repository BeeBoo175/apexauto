import { calendarDate } from "@/lib/calendar";
import { calendarHref, type CalendarFilters } from "./calendar";
import type { StaffFilters } from "./contracts";
import { staffParams, staffViewQuery } from "./query";

export function tableViewHref(filters: CalendarFilters) {
  const table = staffParams({
    from: filters.range?.from,
    to: filters.range?.to,
    status: filters.status,
    branchId: filters.branchId,
  });
  return `/dashboard/intake?${staffViewQuery(table.filters)}`;
}

export function calendarViewHref(filters: StaffFilters, today = calendarDate()) {
  const range = filters.from && filters.to ? { from: filters.from, to: filters.to } : undefined;
  return calendarHref({
    date: range?.from ?? today,
    status: filters.status,
    branchId: filters.branchId,
    range,
  });
}
