import { appointmentStatusLabel } from "@/lib/staff/status";
import { dateRangeLabel } from "@/lib/date-range-label";
import type { StaffFilters } from "./contracts";

export interface AppliedWorklistFilter {
  key: "dates" | "status" | "branch";
  label: string;
  without: StaffFilters;
}

export function resetWorklistFilters(filters: StaffFilters): StaffFilters {
  return { ...filters, from: "", to: "", status: "All", branchId: "", search: "" };
}

export function appliedWorklistFilters(filters: StaffFilters, branchName: string) {
  const applied: AppliedWorklistFilter[] = [];

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
      label: appointmentStatusLabel(filters.status),
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
