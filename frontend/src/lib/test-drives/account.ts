import type { SearchParams } from "@/lib/query";
import { historyParams, historyQuery, type HistoryFilters } from "@/lib/intake/list-query";
import { ACCOUNT_PAGE_SIZE } from "@/lib/pagination";
import { parseTestDriveHistory, parseTestDriveHistoryItem } from "./customer";

export type TestDriveHistory = NonNullable<ReturnType<typeof parseTestDriveHistory>>;
export type TestDriveHistoryItem = NonNullable<ReturnType<typeof parseTestDriveHistoryItem>>;

export function accountTestDriveParams(params: SearchParams) {
  return historyParams({
    page: params.testDrivePage,
    from: params.testDriveFrom,
    to: params.testDriveTo,
  });
}

export function testDriveHistoryQuery(filters: HistoryFilters, page: number) {
  return historyQuery(filters, page, ACCOUNT_PAGE_SIZE);
}

export const testDriveStatusLabels = {
  Booked: "Booked",
  InProgress: "In progress",
  Completed: "Completed",
  NotCompleted: "Not completed",
  NoShow: "No show",
  Cancelled: "Cancelled",
} as const;
