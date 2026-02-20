import { paginationQuery } from "@/lib/pagination";

export interface HistoryFilters {
  from: string;
  to: string;
}

export function historyQuery(filters: HistoryFilters, page = 1) {
  const query = paginationQuery(page);
  if (filters.from) {
    query.set("from", filters.from);
  }
  if (filters.to) {
    query.set("to", filters.to);
  }
  return query.toString();
}

export function dateRangeError({ from, to }: HistoryFilters) {
  if (!from && !to) {
    return "";
  }
  if (!from || !to) {
    return "Choose both From and To dates, or leave both empty.";
  }
  const days = (Date.parse(to) - Date.parse(from)) / 86400000;
  if (!Number.isFinite(days) || days < 0 || days >= 366) {
    return "Choose an inclusive range of at most 366 days, with To on or after From.";
  }
  return "";
}

export function historyParams(params: Record<string, string | string[] | undefined>) {
  const from = typeof params.from === "string" ? params.from : "";
  const to = typeof params.to === "string" ? params.to : "";
  const page = typeof params.page === "string" ? Number(params.page) : 1;
  return { filters: { from, to }, page: Number.isSafeInteger(page) && page > 0 ? page : 1 };
}
