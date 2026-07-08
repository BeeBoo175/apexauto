"use client";

import { useState } from "react";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { Button } from "@/components/ui/Button";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { SearchField } from "@/components/ui/SearchField";
import type { AppliedFilter } from "@/lib/staff/filter-summary";
import { noTeamFilters, type TeamFilters } from "@/lib/performance/team-filters";
import styles from "@/components/staff/StaffWorklist.module.css";

// The search and applied filters above the team table, the same toolbar the other staff tables carry: a search by name
// or branch, a pill for each filter, and Reset filters when any apply. The page's own Filters keep the period and
// branch.
export function TeamTableToolbar({
  filters,
  applied,
  busy,
  onChange,
}: {
  filters: TeamFilters;
  applied: AppliedFilter<TeamFilters>[];
  busy: boolean;
  onChange: (next: TeamFilters) => void;
}) {
  const [draft, setDraft] = useState(filters.search);
  return (
    <RecordToolbar
      filters={null}
      disabled={false}
      filterCount={applied.length}
      filterSummary={
        <AppliedWorklistFilters filters={applied} disabled={busy} onRemove={onChange} />
      }
      actions={
        applied.length > 0 && (
          <Button
            size="compact"
            variant="ghost"
            disabled={busy}
            onClick={() => {
              setDraft("");
              onChange({ ...noTeamFilters, sort: filters.sort });
            }}
          >
            Reset filters
          </Button>
        )
      }
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
