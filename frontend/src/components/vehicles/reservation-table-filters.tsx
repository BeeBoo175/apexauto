import type { DataColumn } from "@/components/ui/data-table/types";
import type { useReservationTable } from "@/hooks/use-reservation-table";
import { dateRangeLabel } from "@/lib/date-range-label";
import type { AppliedWorklistFilter } from "@/lib/staff/filter-summary";
import type { ReservationFilters as Filters, ReservationItem } from "@/lib/vehicles/reservations";
import { reservationStatuses } from "@/lib/vehicles/reservations";
import { ReservationFilters } from "./ReservationFilters";

export function reservationTableFilters(
  state: ReturnType<typeof useReservationTable>,
  columns: DataColumn<ReservationItem>[],
) {
  const filters = state.active.filters;
  const applied: AppliedWorklistFilter<Filters>[] = [];
  if (filters.branchId) {
    applied.push({
      key: "branch",
      label:
        state.branches?.items.find((branch) => branch.id === filters.branchId)?.name ??
        state.snapshot?.items.find((row) => row.branchId === filters.branchId)?.branchName ??
        "Selected branch",
      without: { ...filters, branchId: "" },
    });
  }
  for (const entry of [
    { key: "registration", label: "Registration" },
    { key: "vehicle", label: "Vehicle" },
    { key: "buyer", label: "Buyer" },
  ] as const) {
    if (filters[entry.key]) {
      applied.push({
        key: entry.key,
        label: `${entry.label}: ${filters[entry.key]}`,
        without: { ...filters, [entry.key]: "" },
      });
    }
  }
  if (filters.status) {
    const status = filters.status as keyof typeof reservationStatuses;
    applied.push({
      key: "status",
      label: reservationStatuses[status] ?? filters.status,
      without: { ...filters, status: "" },
    });
  }
  if (filters.minimumTotal || filters.maximumTotal) {
    applied.push({
      key: "total",
      label: `Reserved total: MYR ${filters.minimumTotal || "0"}–${filters.maximumTotal || "any"}`,
      without: { ...filters, minimumTotal: "", maximumTotal: "" },
    });
  }
  const ranges = [
    { key: "created", label: "Created", from: "createdFrom", to: "createdTo" },
    { key: "expires", label: "Expires", from: "expiresFrom", to: "expiresTo" },
    { key: "released", label: "Released", from: "releasedFrom", to: "releasedTo" },
  ] as const;
  for (const range of ranges) {
    const from = filters[range.from] ?? "";
    const to = filters[range.to] ?? "";
    if (from || to) {
      applied.push({
        key: range.key,
        label: `${range.label}: ${dateRangeLabel(from, to)}`,
        without: { ...filters, [range.from]: "", [range.to]: "" },
      });
    }
  }
  const indicators: Record<string, boolean> = {
    registration: Boolean(filters.registration),
    vehicle: Boolean(filters.vehicle),
    buyer: Boolean(filters.buyer),
    status: Boolean(filters.status),
    branch: Boolean(filters.branchId),
    total: Boolean(filters.minimumTotal || filters.maximumTotal),
    created: Boolean(filters.createdFrom || filters.createdTo),
    expires: Boolean(filters.expiresFrom || filters.expiresTo),
    released: Boolean(filters.releasedFrom || filters.releasedTo),
  };
  const filteredColumns = columns.map((column) => ({
    ...column,
    filter: {
      active: indicators[column.id],
      content: <ReservationFilters state={state} column={column.id} />,
    },
  }));
  return { columns: filteredColumns, applied };
}
