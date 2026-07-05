"use client";

import { useState } from "react";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { BranchFilter } from "@/components/staff/BranchFilter";
import { Button } from "@/components/ui/Button";
import { ColumnFilterForm } from "@/components/ui/data-table/ColumnFilterForm";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import performance from "@/components/performance/Performance.module.css";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { AppliedFilter } from "@/lib/staff/filter-summary";

// A general manager's Home filter, built like the table pages: the Filters popover with Clear and Apply, the chosen
// branch as a removable pill and Reset filters beside it.
export function HomeBranchToolbar({
  branches,
  branchId,
  busy,
  onBranch,
}: {
  branches: Page<Branch> | null;
  branchId: string;
  busy: boolean;
  onBranch: (branchId: string) => void;
}) {
  const applied: AppliedFilter<string>[] = branchId
    ? [
        {
          key: "branch",
          label: branches?.items.find((item) => item.id === branchId)?.name ?? "Selected branch",
          without: "",
        },
      ]
    : [];
  return (
    <RecordToolbar
      disabled={busy}
      summaryInline
      filterCount={applied.length}
      filterSummary={
        <AppliedWorklistFilters filters={applied} disabled={busy} onRemove={onBranch} />
      }
      filters={
        <BranchForm
          key={branchId}
          branches={branches}
          branchId={branchId}
          busy={busy}
          onBranch={onBranch}
        />
      }
      actions={
        applied.length > 0 && (
          <Button size="compact" variant="ghost" disabled={busy} onClick={() => onBranch("")}>
            Reset filters
          </Button>
        )
      }
    />
  );
}

function BranchForm({
  branches,
  branchId,
  busy,
  onBranch,
}: {
  branches: Page<Branch> | null;
  branchId: string;
  busy: boolean;
  onBranch: (branchId: string) => void;
}) {
  const [draft, setDraft] = useState(branchId);
  return (
    <ColumnFilterForm
      busy={busy}
      onApply={async () => {
        onBranch(draft);
        return true;
      }}
      onClear={async () => {
        onBranch("");
        return true;
      }}
    >
      <div className={performance.filterForm}>
        <BranchFilter
          branches={branches}
          branchId={draft}
          disabled={busy}
          onChange={setDraft}
          onPage={() => {}}
          emptyLabel="All branches"
          labelHidden={false}
        />
      </div>
    </ColumnFilterForm>
  );
}
