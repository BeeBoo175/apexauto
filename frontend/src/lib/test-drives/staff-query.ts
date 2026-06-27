import type { SearchParams } from "@/lib/query";
import { DEFAULT_PAGE_SIZE, isPageSize, paginationQuery } from "@/lib/pagination";
import { isUuid } from "@/lib/auth/contracts";
import { isCalendarDate } from "@/lib/calendar";
import { dateRangeError, type HistoryFilters } from "@/lib/intake/list-query";
import { testDriveStatusLabels } from "./account";
import type { parseStaffTestDriveDetail, parseStaffTestDrivePage } from "./staff";

export type StaffTestDrivePage = NonNullable<ReturnType<typeof parseStaffTestDrivePage>>;
export type StaffTestDriveItem = StaffTestDrivePage["items"][number];
export type StaffTestDriveDetail = NonNullable<ReturnType<typeof parseStaffTestDriveDetail>>;
export interface TestDriveFilters {
  from: string;
  to: string;
  queue: string;
  status: string;
  branchId: string;
  handlerId: string;
  handler: string;
  registration: string;
  vehicle: string;
  customer: string;
  search: string;
  sort: string;
  direction: "asc" | "desc";
  pageSize: number;
}

export const testDriveQueues = [
  { value: "ToClaim", label: "To claim", count: "toClaim" },
  { value: "MyWork", label: "My work", count: "myWork" },
  { value: "History", label: "History", count: "history" },
] as const;

const terminalTestDriveStatuses = ["Completed", "NotCompleted", "Cancelled", "NoShow"];

export function isTerminalTestDriveStatus(status: string) {
  return terminalTestDriveStatuses.includes(status);
}

export function testDriveQueue(detail: StaffTestDriveDetail) {
  if (isTerminalTestDriveStatus(detail.booking.status)) {
    return "History";
  }
  return detail.booking.handlerId ? "MyWork" : "ToClaim";
}

export const testDriveSections = [
  { value: "All", label: "All test drives", count: "all" },
  { value: "Booked", label: "Booked", count: "booked" },
  { value: "InProgress", label: "In progress", count: "inProgress" },
  { value: "Completed", label: "Completed", count: "completed" },
  { value: "NotCompleted", label: "Not completed", count: "notCompleted" },
  { value: "NoShow", label: "No shows", count: "noShow" },
  { value: "Cancelled", label: "Cancelled", count: "cancelled" },
] as const;

const sortKeys = [
  "startsAt",
  "registrationPlate",
  "vehicle",
  "customerName",
  "branchName",
  "status",
  "handlerEmail",
];

const cleanText = (value: string) => value.replace(/[\x00-\x1f\x7f-\x9f]/g, "").slice(0, 160);

export function staffTestDriveParams(parameters: SearchParams, agent: boolean) {
  const text = (key: string) => (typeof parameters[key] === "string" ? parameters[key] : "");
  const page = Number(text("page") || 1);
  const pageSize = Number(text("pageSize"));
  const requestedQueue = testDriveQueues.find((item) => item.value === text("queue"));
  const status = text("status");
  return {
    filters: {
      from: agent ? "" : text("from"),
      to: agent ? "" : text("to"),
      queue: agent ? (requestedQueue?.value ?? "MyWork") : "",
      status: !agent && Object.hasOwn(testDriveStatusLabels, status) ? status : "All",
      branchId: !agent && isUuid(text("branchId")) ? text("branchId") : "",
      handlerId: !agent && isUuid(text("handlerId")) ? text("handlerId") : "",
      handler: agent ? "" : cleanText(text("handler")),
      registration: agent ? "" : cleanText(text("registration")),
      vehicle: agent ? "" : cleanText(text("vehicle")),
      customer: agent ? "" : cleanText(text("customer")),
      search: text("search")
        .replace(/[\x00-\x1f\x7f-\x9f]/g, "")
        .slice(0, 160),
      sort: !agent && sortKeys.includes(text("sort")) ? text("sort") : "",
      direction: !agent && text("direction") === "desc" ? ("desc" as const) : ("asc" as const),
      pageSize: isPageSize(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
    },
    page: Number.isSafeInteger(page) && page > 0 ? page : 1,
    selectedId: isUuid(text("booking")) ? text("booking") : "",
  };
}

export function staffTestDriveQuery(filters: TestDriveFilters, page = 1) {
  const query = paginationQuery(page, filters.pageSize);
  for (const [key, value] of Object.entries(filters)) {
    if (key !== "pageSize" && value !== "") {
      query.set(key, String(value));
    }
  }
  return query.toString();
}

export const testDriveSectionParameter = "section";

export function staffTestDriveUrl(
  filters: TestDriveFilters,
  page: number,
  selectedId = "",
  section = "",
) {
  const query = new URLSearchParams(staffTestDriveQuery(filters, page));
  if (selectedId) {
    query.set("booking", selectedId);
    if (section) {
      query.set(testDriveSectionParameter, section);
    }
  }
  return `/dashboard/test-drives?${query}`;
}

export function testDriveDateError(filters: HistoryFilters) {
  const invalid = [filters.from, filters.to].some((date) => date && !isCalendarDate(date));
  return invalid ? "Choose valid From and To dates, or leave both empty." : dateRangeError(filters);
}
