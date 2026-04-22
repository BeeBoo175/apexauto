import { useId, useState } from "react";
import { Input, Select } from "@/components/ui/Input";
import { BranchFilter } from "@/components/staff/BranchFilter";
import { FilterChoices } from "@/components/ui/FilterChoices";
import {
  ColumnFilterForm,
  DateRangeFilter,
  MoneyRangeFilter,
  TextColumnFilter,
} from "@/components/ui/data-table/ColumnFilterForm";
import { workshopStatuses, workshopStatusLabel } from "@/lib/inspection/workshop-contracts";
import type { QueueFilters as Filters } from "@/lib/inspection/contracts";
import type { Branch } from "@/lib/branches";
import type { Page } from "@/lib/api";

export function QueueFilters({
  filters,
  column,
  apply,
  busy,
  branches,
  loadBranches,
}: {
  filters: Filters;
  column: string;
  apply: (value: Filters) => Promise<boolean | undefined>;
  busy: boolean;
  branches: Page<Branch> | null;
  loadBranches: (page: number) => void;
}) {
  const id = useId();
  const [draft, setDraft] = useState(filters);
  const [unassigned, setUnassigned] = useState(filters.technician === "unassigned");
  if (column === "registrationPlate" || column === "vehicle") {
    const key = column === "vehicle" ? "vehicle" : "registration";
    return (
      <TextColumnFilter
        label={key === "registration" ? "Registration contains" : "Vehicle contains"}
        value={filters[key] ?? ""}
        busy={busy}
        onApply={(value) => apply({ ...filters, [key]: value })}
      />
    );
  }
  if (column === "checkedInAt" || column === "requestedAt") {
    return (
      <DateRangeFilter
        from={filters.from}
        to={filters.to}
        busy={busy}
        onApply={(from, to) => apply({ ...filters, from, to })}
      />
    );
  }
  if (column === "budget") {
    return (
      <MoneyRangeFilter
        minimum={filters.minimumBudget ?? ""}
        maximum={filters.maximumBudget ?? ""}
        busy={busy}
        onApply={(minimumBudget, maximumBudget) =>
          apply({ ...filters, minimumBudget, maximumBudget })
        }
      />
    );
  }
  function next(clear: boolean) {
    if (column === "branchName") {
      return { ...filters, branchId: clear ? "" : draft.branchId };
    }
    if (column === "status") {
      return { ...filters, workStatus: clear ? "" : draft.workStatus };
    }
    if (column === "technicianIdentity") {
      let technician = draft.technician?.trim() ?? "";
      if (unassigned) {
        technician = "unassigned";
      }
      return { ...filters, technician: clear ? "" : technician };
    }
    return { ...filters, from: clear ? "" : draft.from, to: clear ? "" : draft.to };
  }
  return (
    <ColumnFilterForm
      busy={busy}
      onApply={() => apply(next(false))}
      onClear={() => apply(next(true))}
    >
      {column === "branchName" && (
        <BranchFilter
          branches={branches}
          branchId={draft.branchId}
          disabled={busy}
          labelHidden
          onChange={(branchId) => setDraft({ ...draft, branchId })}
          onPage={loadBranches}
        />
      )}
      {column === "status" && (
        <FilterChoices
          label="Current work"
          options={[
            { value: "", label: "All" },
            ...workshopStatuses.map((value) => ({ value, label: workshopStatusLabel(value) })),
          ]}
          value={draft.workStatus ?? ""}
          disabled={busy}
          onChange={(workStatus) => setDraft({ ...draft, workStatus })}
        />
      )}
      {column === "technicianIdentity" && (
        <>
          <Select
            label="Assignment"
            density="compact"
            value={unassigned ? "unassigned" : "identity"}
            disabled={busy}
            onChange={(event) => {
              setUnassigned(event.target.value === "unassigned");
              setDraft({ ...draft, technician: "" });
            }}
          >
            <option value="identity">Any technician</option>
            <option value="unassigned">Unassigned</option>
          </Select>
          {!unassigned && (
            <Input
              id={`${id}-technician`}
              label="Search technician"
              labelHidden
              placeholder="Enter technician name or email…"
              density="compact"
              maxLength={160}
              value={draft.technician ?? ""}
              disabled={busy}
              onChange={(event) => setDraft({ ...draft, technician: event.target.value })}
            />
          )}
        </>
      )}
    </ColumnFilterForm>
  );
}
