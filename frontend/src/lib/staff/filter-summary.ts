import { appointmentStatusLabel } from "@/lib/staff/status";
import { dateRangeLabel } from "@/lib/date-range-label";
import type { StaffFilters } from "./contracts";

export interface AppliedWorklistFilter<Filters = StaffFilters> {
  key: "dates" | "status" | "branch";
  label: string;
  without: Filters;
}

export function resetWorklistFilters(filters: StaffFilters): StaffFilters {
  return { ...filters, from: "", to: "", status: "All", branchId: "", search: "" };
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
