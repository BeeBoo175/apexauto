import { isUuid } from "@/lib/auth/contracts";
import { isPageSize } from "@/lib/pagination";
import { inspectionStatuses, repairStatuses, type QueueFilters, type QueueKind } from "./contracts";

export function queueParams(
  params: Record<string, string | string[] | undefined>,
  kind: QueueKind,
) {
  const get = (key: string) => (typeof params[key] === "string" ? params[key] : "");
  const statuses = kind === "vehicles" ? inspectionStatuses : repairStatuses;
  const page = Number(get("page") || 1);
  const size = Number(get("pageSize") || 25);
  const filters: QueueFilters = {
    search: get("search"),
    status: statuses.find((s) => s === get("status")) ?? "All",
    branchId: isUuid(get("branchId")) ? get("branchId") : "",
    from: get("from"),
    to: get("to"),
    pageSize: isPageSize(size) ? size : 25,
  };
  return { filters, page: Number.isSafeInteger(page) && page > 0 ? page : 1 };
}

export function queueQuery(filters: QueueFilters, page: number) {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: String(filters.pageSize),
    status: filters.status,
  });
  for (const key of ["search", "branchId", "from", "to"] as const) {
    if (filters[key]) {
      query.set(key, filters[key]);
    }
  }
  return query.toString();
}

export function parseMyr(value: string): number | null {
  if (!/^\d+(?:\.\d{1,2})?$/.test(value)) {
    return null;
  }
  const [whole, cents = ""] = value.split(".");
  const normalized = `${BigInt(whole)}.${cents.padEnd(2, "0")}`;
  const number = Number(normalized);
  if (
    !Number.isFinite(number) ||
    number * 100 > Number.MAX_SAFE_INTEGER ||
    number.toFixed(2) !== normalized
  ) {
    return null;
  }
  return number;
}
