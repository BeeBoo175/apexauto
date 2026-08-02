import { isUuid } from "@/lib/auth/contracts";
import { staffViewQuery } from "./query";
import type { StaffFilters } from "./contracts";

export function intakeLocation(filters: StaffFilters, page: number, id = "") {
  const path = id ? `/dashboard/intake/${id}` : "/dashboard/intake";
  const query = staffViewQuery(filters, page);
  return query ? `${path}?${query}` : path;
}

export function intakeLocationId(pathname: string) {
  if (pathname === "/dashboard/intake") {
    return "";
  }
  const match = /^\/dashboard\/intake\/([^/]+)$/.exec(pathname);
  return match && isUuid(match[1]) ? match[1] : null;
}
