"use client";

import { useState } from "react";
import { Select } from "@/components/ui/Input";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { SearchField } from "@/components/ui/SearchField";
import type { CurrentActor } from "@/lib/auth/actor";
import type { useWorkCalendar } from "@/hooks/use-work-calendar";
import { BranchFilter } from "./BranchFilter";
import styles from "./WorkCalendar.module.css";

export function WorkCalendarFilters({
  actor,
  calendar,
}: {
  actor: CurrentActor;
  calendar: ReturnType<typeof useWorkCalendar>;
}) {
  const { filters, pending, branches } = calendar;
  const [search, setSearch] = useState(filters.search);
  const busy = pending.scheduled || pending.unscheduled;
  return (
    <div
      className={styles.sharedFilters}
      role="group"
      aria-label="Filters for ongoing work and appointments"
    >
      <RecordToolbar
        filters={null}
        filterCount={0}
        filterSummary={null}
        disabled={busy}
        actions={
          <div className={styles.filterActions}>
            {actor.role === "GeneralManager" && (
              <div className={styles.branchControl}>
                <BranchFilter
                  branches={branches}
                  branchId={filters.branchId}
                  emptyLabel="All branches"
                  showTitle
                  disabled={busy || pending.branches}
                  selectedBranchName={
                    calendar.scheduled.data?.items.find(
                      (item) => item.branchId === filters.branchId,
                    )?.branchName ??
                    calendar.unscheduled.data?.items.find(
                      (item) => item.branchId === filters.branchId,
                    )?.branchName
                  }
                  onChange={(branchId) =>
                    void calendar.changeScope({ ...filters, branchId, page: 1 })
                  }
                  onPage={(page) => void calendar.loadBranches(page)}
                />
              </div>
            )}
            {actor.role !== "SalesAgent" && (
              <div className={styles.assignmentControl}>
                <Select
                  label="Appointment assignment (appointments only)"
                  labelHidden
                  density="compact"
                  value={calendar.scheduled.filters.assignment}
                  disabled={pending.scheduled}
                  onChange={(event) => {
                    const assignment = event.target.value;
                    if (
                      assignment === "all" ||
                      assignment === "assigned" ||
                      assignment === "unassigned"
                    ) {
                      void calendar.changeAssignment(assignment);
                    }
                  }}
                >
                  <option value="all">Appointments: all</option>
                  <option value="assigned">Appointments: assigned</option>
                  <option value="unassigned">Appointments: unassigned</option>
                </Select>
              </div>
            )}
          </div>
        }
      >
        <form
          className={styles.sharedSearch}
          onSubmit={(event) => {
            event.preventDefault();
            if (!busy) {
              void calendar.changeScope({ ...filters, search, page: 1 });
            }
          }}
        >
          <SearchField
            label="Search ongoing work and appointments"
            placeholder={
              actor.role === "SalesAgent"
                ? "Search all my work"
                : "Search all branch work, customers or staff"
            }
            value={search}
            disabled={false}
            busy={busy}
            onChange={setSearch}
            onClear={
              filters.search && !busy
                ? () => {
                    setSearch("");
                    void calendar.changeScope({ ...filters, search: "", page: 1 });
                  }
                : undefined
            }
          />
        </form>
      </RecordToolbar>
      <span className="sr-only">
        Branch and submitted search apply to both panels. Other controls affect only their own
        panel.
      </span>
    </div>
  );
}
