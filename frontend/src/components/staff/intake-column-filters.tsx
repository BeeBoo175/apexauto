import { useState } from "react";
import {
  ColumnFilterForm,
  DateRangeFilter,
  TextColumnFilter,
} from "@/components/ui/data-table/ColumnFilterForm";
import { FilterChoices } from "@/components/ui/FilterChoices";
import type { DataColumn } from "@/components/ui/data-table/types";
import type { useStaffWorklist } from "@/hooks/use-staff-worklist";
import { worklistStatuses, type StaffFilters, type WorklistItem } from "@/lib/staff/contracts";
import { appointmentStatusLabel } from "@/lib/staff/status";
import { BranchFilter } from "./BranchFilter";

function IntakeColumnFilter({
  worklist,
  column,
  disabled,
}: {
  worklist: ReturnType<typeof useStaffWorklist>;
  column: string;
  disabled: boolean;
}) {
  const filters = worklist.active.filters;
  const [draft, setDraft] = useState(filters);
  const [lastFilters, setLastFilters] = useState(filters);
  if (lastFilters !== filters) {
    setLastFilters(filters);
    setDraft(filters);
  }
  const busy = disabled || worklist.busy;
  const apply = (next: StaffFilters) => worklist.load(next, 1);

  if (
    column === "registrationPlate" ||
    column === "vehicle" ||
    column === "customerName" ||
    column === "handlerEmail"
  ) {
    const keys = {
      registrationPlate: { key: "registration", label: "Registration contains" },
      vehicle: { key: "vehicle", label: "Vehicle contains" },
      customerName: { key: "customer", label: "Customer contains" },
      handlerEmail: { key: "handler", label: "Handler email contains" },
    } as const;
    const { key, label } = keys[column];
    return (
      <TextColumnFilter
        label={label}
        value={filters[key] ?? ""}
        busy={busy}
        onApply={(value) => apply({ ...filters, [key]: value })}
      />
    );
  }

  if (column === "startsAt") {
    return (
      <DateRangeFilter
        from={filters.from}
        to={filters.to}
        busy={busy}
        onApply={(from, to) => apply({ ...filters, from, to })}
      />
    );
  }

  const branch = column === "branchName";
  const key = branch ? "branchId" : "status";
  return (
    <ColumnFilterForm
      busy={busy}
      onApply={() => apply({ ...filters, [key]: draft[key] })}
      onClear={() => apply({ ...filters, [key]: branch ? "" : "all" })}
    >
      {branch ? (
        <BranchFilter
          branches={worklist.branches}
          branchId={draft.branchId}
          selectedBranchName={
            worklist.snapshot?.items.find((row) => row.branchId === draft.branchId)?.branchName
          }
          disabled={busy}
          labelHidden={false}
          onChange={(branchId) => setDraft({ ...draft, branchId })}
          onPage={(page) => void worklist.loadBranches(page)}
        />
      ) : (
        <FilterChoices
          label="Status"
          options={worklistStatuses.map((status) => ({
            value: status,
            label: status === "all" ? "All statuses" : appointmentStatusLabel(status),
          }))}
          value={draft.status}
          disabled={busy}
          onChange={(status) => setDraft({ ...draft, status })}
        />
      )}
    </ColumnFilterForm>
  );
}

export function intakeColumnFilters(
  worklist: ReturnType<typeof useStaffWorklist>,
  columns: DataColumn<WorklistItem>[],
  group: boolean,
  disabled: boolean,
) {
  const filters = worklist.active.filters;
  const indicators: Record<string, boolean> = {
    startsAt: Boolean(filters.from || filters.to),
    status: filters.status !== "all",
    registrationPlate: Boolean(filters.registration),
    vehicle: Boolean(filters.vehicle),
    customerName: Boolean(filters.customer),
    handlerEmail: Boolean(filters.handler || filters.handlerId),
  };
  if (group) {
    indicators.branchName = Boolean(filters.branchId);
  }
  return columns.map((column) => {
    if (!(column.id in indicators)) {
      return column;
    }
    return {
      ...column,
      filter: {
        active: indicators[column.id],
        content: <IntakeColumnFilter worklist={worklist} column={column.id} disabled={disabled} />,
      },
    };
  });
}
