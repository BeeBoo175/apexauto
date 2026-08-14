import { isUuid } from "@/lib/auth/contracts";
import { staffViewQuery } from "./query";
import type { StaffFilters } from "./contracts";

// The new-intake drawer is part of the address (?create=intake), so a reload or a shared link reopens it.
export const INTAKE_CREATE_KEY = "create";
export const INTAKE_CREATE_TOKEN = "intake";

export const isIntakeCreating = (search: string) =>
  new URLSearchParams(search).get(INTAKE_CREATE_KEY) === INTAKE_CREATE_TOKEN;

export function intakeLocation(filters: StaffFilters, page: number, id = "", creating = false) {
  const path = id ? `/dashboard/intake/${id}` : "/dashboard/intake";
  const query = new URLSearchParams(staffViewQuery(filters, page));
  if (creating) {
    query.set(INTAKE_CREATE_KEY, INTAKE_CREATE_TOKEN);
  }
  const text = query.toString();
  return text ? `${path}?${text}` : path;
}

export function intakeLocationId(pathname: string) {
  if (pathname === "/dashboard/intake") {
    return "";
  }
  const match = /^\/dashboard\/intake\/([^/]+)$/.exec(pathname);
  return match && isUuid(match[1]) ? match[1] : null;
}
