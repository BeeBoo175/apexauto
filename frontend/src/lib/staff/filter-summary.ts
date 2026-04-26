import { appointmentStatusLabel } from "@/lib/staff/status";
import { dateRangeLabel } from "@/lib/date-range-label";
import type { StaffFilters } from "./contracts";

export interface AppliedWorklistFilter<Filters = StaffFilters> {
  key:
    | "dates"
    | "status"
    | "branch"
    | "inspection"
    | "publication"
    | "price"
    | "publishedPrice"
    | "budget"
    | "technician"
    | "workStatus"
    | "registration"
    | "vehicle"
    | "customer"
    | "buyer"
    | "created"
    | "expires"
    | "released"
    | "total"
    | "code"
    | "name"
    | "discount"
    | "grade";
  label: string;
  without: Filters;
}

export function resetWorklistFilters(filters: StaffFilters): StaffFilters {
  return {
    ...filters,
    from: "",
    to: "",
    status: "All",
    branchId: "",
    search: "",
    registration: "",
    vehicle: "",
    customer: "",
  };
}

export function appliedWorklistFilters<
  Filters extends { from: string; to: string; status: string; branchId: string },
>(filters: Filters, branchName: string, statusLabel = appointmentStatusLabel) {
  const applied: AppliedWorklistFilter<Filters>[] = [];

  if (filters.from || filters.to) {
    applied.push({
      key: "dates",
      label: dateRangeLabel(filters.from, filters.to),
      without: { ...filters, from: "", to: "" },
    });
  }

  if (filters.status !== "All") {
    applied.push({
      key: "status",
      label: statusLabel(filters.status),
      without: { ...filters, status: "All" },
    });
  }

  if (filters.branchId) {
    applied.push({
      key: "branch",
      label: branchName,
      without: { ...filters, branchId: "" },
    });
  }

  return applied;
}

export function appliedIntakeFilters(filters: StaffFilters, branchName: string) {
  const applied = appliedWorklistFilters(filters, branchName).filter(
    (filter) => filter.key !== "status",
  );
  const labels = { registration: "Registration", vehicle: "Vehicle", customer: "Customer" };
  for (const key of ["registration", "vehicle", "customer"] as const) {
    if (filters[key]) {
      applied.push({
        key,
        label: `${labels[key]}: ${filters[key]}`,
        without: { ...filters, [key]: "" },
      });
    }
  }
  return applied;
}
