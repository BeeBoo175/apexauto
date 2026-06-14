"use client";

import { useState } from "react";
import { type Page } from "@/lib/api";
import { type Branch } from "@/lib/branches";
import { BranchFilter } from "@/components/staff/BranchFilter";
import {
  ColumnFilterForm,
  DateRangeFilter,
  TextColumnFilter,
} from "@/components/ui/data-table/ColumnFilterForm";
import type { useStaffTestDriveList } from "@/hooks/use-staff-test-drive-list";
import { useBranchChoices } from "@/hooks/use-branch-choices";

export function StaffTestDriveFilters({
  list,
  column,
  initialBranches,
  disabled,
}: {
  list: ReturnType<typeof useStaffTestDriveList>;
  column: string;
  initialBranches: Page<Branch> | null;
  disabled: boolean;
}) {
  const { filters } = list.active;
  const [branchId, setBranchId] = useState(filters.branchId);
  const branchChoices = useBranchChoices(
    initialBranches,
    25,
    "Branch choices could not be refreshed. Retry to load branches.",
  );
  const { branches, busy } = branchChoices;

  if (column === "startsAt") {
    return (
      <DateRangeFilter
        from={filters.from}
        to={filters.to}
        busy={disabled}
        onApply={(from, to) => list.load({ ...filters, from, to }, 1)}
      />
    );
  }
  if (column === "handlerEmail") {
    return (
      <TextColumnFilter
        label="Handler email contains"
        value={filters.handler}
        busy={disabled}
        onApply={(handler) => list.load({ ...filters, handler, handlerId: "" }, 1)}
      />
    );
  }
  return (
    <ColumnFilterForm
      busy={disabled || busy}
      onApply={() => list.load({ ...filters, branchId }, 1)}
      onClear={() => list.load({ ...filters, branchId: "" }, 1)}
    >
      <BranchFilter
        branches={branches}
        branchId={branchId}
        selectedBranchName={
          list.snapshot.items.find((row) => row.branchId === branchId)?.branchName
        }
        disabled={disabled || busy}
        labelHidden={false}
        onChange={setBranchId}
        onPage={(page) => void branchChoices.load(page)}
      />
    </ColumnFilterForm>
  );
}
