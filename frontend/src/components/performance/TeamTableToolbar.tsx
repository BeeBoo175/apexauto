"use client";

import { useState } from "react";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { ResetFiltersButton } from "@/components/ui/ResetFiltersButton";
import { TableDiscovery } from "@/components/ui/data-table/TableDiscovery";
import type { DataColumn, TableSorting } from "@/components/ui/data-table/types";
import { SearchField } from "@/components/ui/SearchField";
import type { AppliedFilter } from "@/lib/staff/filter-summary";
import { noTeamFilters, type TeamFilters } from "@/lib/performance/team-filters";
import styles from "@/components/staff/StaffWorklist.module.css";

// The search and applied filters above the team table, the same toolbar the other staff tables carry: a search by name
// or branch, a pill for each filter, and Reset filters when any apply. The page's own Filters keep the period and
// branch.
export function TeamTableToolbar<Row>({
  filters,
  applied,
  busy,
  columns,
  sorting,
  onChange,
}: {
  columns: readonly DataColumn<Row>[];
  sorting: TableSorting;
  filters: TeamFilters;
  applied: AppliedFilter<TeamFilters>[];
  busy: boolean;
  onChange: (next: TeamFilters) => void;
}) {
  const [draft, setDraft] = useState(filters.search);

  function reset() {
    setDraft("");
    onChange({ ...noTeamFilters, sort: filters.sort });
  }

  return (
    <RecordToolbar
      filters={null}
      mobileDiscovery={
        <TableDiscovery
          label="people"
          columns={columns}
          sorting={sorting}
          filterCount={applied.length}
          disabled={busy}
          onClearFilters={reset}
        />
      }
      disabled={false}
      filterCount={applied.length}
      filterSummary={
        <AppliedWorklistFilters filters={applied} disabled={busy} onRemove={onChange} />
      }
      actions={applied.length > 0 && <ResetFiltersButton disabled={busy} onClick={reset} />}
    >
      <form
        className={styles.search}
        onSubmit={(event) => {
          event.preventDefault();
          if (!busy) {
            onChange({ ...filters, search: draft.trim() });
          }
        }}
      >
        <SearchField
          label="Search people"
          placeholder="Search name or branch"
          value={draft}
          disabled={false}
          busy={busy}
          onChange={setDraft}
          onClear={
            filters.search && !busy
              ? () => {
                  setDraft("");
                  onChange({ ...filters, search: "" });
                }
              : undefined
          }
        />
      </form>
    </RecordToolbar>
  );
}
