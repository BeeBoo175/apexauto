"use client";

import { useEffect, useRef, useState } from "react";
import { type Page } from "@/lib/api";
import { type Branch } from "@/lib/branches";
import { useToast } from "@/components/ui/Toast";
import { BranchFilter } from "@/components/staff/BranchFilter";
import {
  ColumnFilterForm,
  DateRangeFilter,
  TextColumnFilter,
} from "@/components/ui/data-table/ColumnFilterForm";
import type { useStaffTestDriveList } from "@/hooks/use-staff-test-drive-list";
import { readBranchChoices } from "@/lib/branch-choices";

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
  const [branches, setBranches] = useState(initialBranches);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  useEffect(() => () => controller.current?.abort(), []);

  async function loadBranches(page: number) {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await readBranchChoices(page, attempt.signal, 25);
      if (!attempt.signal.aborted && result.data) {
        setBranches(result.data);
      } else if (!attempt.signal.aborted) {
        error("Branch choices could not be refreshed. Retry to load branches.");
      }
    } catch {
      if (!attempt.signal.aborted) {
        error("Branch choices could not be refreshed. Retry to load branches.");
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

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
        onPage={(page) => void loadBranches(page)}
      />
    </ColumnFilterForm>
  );
}
