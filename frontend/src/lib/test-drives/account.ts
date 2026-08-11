import type { SearchParams } from "@/lib/query";
import { historyParams, historyQuery, type HistoryFilters } from "@/lib/intake/list-query";
import { ACCOUNT_PAGE_SIZE } from "@/lib/pagination";
import { parseTestDriveHistory, parseTestDriveHistoryItem } from "./customer";

export type TestDriveHistory = NonNullable<ReturnType<typeof parseTestDriveHistory>>;
export type TestDriveHistoryItem = NonNullable<ReturnType<typeof parseTestDriveHistoryItem>>;

export interface TestDriveHistoryFilters extends HistoryFilters {
  search: string;
}

export function accountTestDriveParams(params: SearchParams) {
  const query = historyParams(params);
  return {
    ...query,
    filters: {
      ...query.filters,
      search: typeof params.search === "string" ? params.search.slice(0, 160) : "",
    },
  };
}

export function testDriveHistoryQuery(filters: TestDriveHistoryFilters, page: number) {
  const query = new URLSearchParams(historyQuery(filters, page, ACCOUNT_PAGE_SIZE));
  if (filters.search.trim()) {
    query.set("search", filters.search.trim());
  }
  return query.toString();
}

export const testDriveStatusLabels = {
  Booked: "Booked",
  InProgress: "In progress",
  Completed: "Completed",
  NotCompleted: "Not completed",
  NoShow: "No show",
  Cancelled: "Cancelled",
} as const;
