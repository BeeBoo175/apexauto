import { useState } from "react";
import {
  ColumnFilterForm,
  DateRangeFilter,
  MoneyRangeFilter,
  TextColumnFilter,
} from "@/components/ui/data-table/ColumnFilterForm";
import { BranchFilter } from "@/components/staff/BranchFilter";
import { FilterChoices } from "@/components/ui/FilterChoices";
import type { useReservationTable } from "@/hooks/use-reservation-table";
import { reservationStatuses } from "@/lib/vehicles/reservations";

export function ReservationFilters({
  state,
  column,
}: {
  state: ReturnType<typeof useReservationTable>;
  column: string;
}) {
  const filters = state.active.filters;
  const [draft, setDraft] = useState(filters);
  const apply = (value: typeof filters) => state.load(value, 1);

  if (
    column === "registration" ||
    column === "vehicle" ||
    column === "buyer" ||
    column === "agent"
  ) {
    const labels = {
      registration: "Registration contains",
      vehicle: "Vehicle contains",
      buyer: "Buyer contains",
      agent: "Agent contains",
    };
    return (
      <TextColumnFilter
        label={labels[column]}
        value={filters[column] ?? ""}
        busy={state.busy}
        onApply={(value) => apply({ ...filters, [column]: value })}
      />
    );
  }
  if (column === "total") {
    return (
      <MoneyRangeFilter
        minimum={filters.minimumTotal ?? ""}
        maximum={filters.maximumTotal ?? ""}
        busy={state.busy}
        onApply={(minimumTotal, maximumTotal) => apply({ ...filters, minimumTotal, maximumTotal })}
      />
    );
  }
  if (column === "created" || column === "expires" || column === "released") {
    const keys = {
      created: { from: "createdFrom", to: "createdTo" },
      expires: { from: "expiresFrom", to: "expiresTo" },
      released: { from: "releasedFrom", to: "releasedTo" },
    } as const;
    const { from, to } = keys[column];
    return (
      <DateRangeFilter
        from={filters[from] ?? ""}
        to={filters[to] ?? ""}
        busy={state.busy}
        onApply={(first, last) => apply({ ...filters, [from]: first, [to]: last })}
      />
    );
  }

  const branch = column === "branch";
  const key = branch ? "branchId" : "status";
  return (
    <ColumnFilterForm
      busy={state.busy}
      onApply={() => apply({ ...filters, [key]: draft[key] })}
      onClear={() => apply({ ...filters, [key]: "" })}
    >
      {branch ? (
        <BranchFilter
          branches={state.branches}
          branchId={draft.branchId}
          selectedBranchName={
            state.snapshot?.items.find((row) => row.branchId === draft.branchId)?.branchName
          }
          disabled={state.busy}
          labelHidden
          onChange={(branchId) => setDraft({ ...draft, branchId })}
          onPage={(page) => void state.loadBranches(page)}
        />
      ) : (
        <FilterChoices
          label="Status"
          options={[
            { value: "", label: "All in this view" },
            ...Object.entries(reservationStatuses)
              .filter(([value]) => value !== "Active")
              .map(([value, label]) => ({ value, label })),
          ]}
          value={draft.status ?? ""}
          disabled={state.busy}
          onChange={(status) => setDraft({ ...draft, status })}
        />
      )}
    </ColumnFilterForm>
  );
}
