"use client";

import { useEffect, useRef, useState } from "react";
import { SearchField } from "@/components/ui/SearchField";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { TableSort } from "@/components/ui/data-table/types";
import { useStaffWorklist } from "@/hooks/use-staff-worklist";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { useStaffAppointmentDrawer } from "@/hooks/use-staff-appointment-drawer";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Branch, Page } from "@/lib/intake/contracts";
import {
  type AppointmentDetail,
  type StaffFilters,
  type WorklistItem,
} from "@/lib/staff/contracts";
import { staffViewQuery } from "@/lib/staff/query";
import { calendarViewHref } from "@/lib/staff/view-navigation";
import { exportWorklist } from "@/lib/staff/export";
import { appliedWorklistFilters } from "@/lib/staff/filter-summary";
import { AppliedWorklistFilters } from "./AppliedWorklistFilters";
import { WorklistFilters } from "./WorklistFilters";
import { IntakeAppointmentDrawer } from "./IntakeAppointmentDrawer";
import { IntakeViewNavigation } from "./IntakeViewNavigation";
import { StaffCommandBar } from "./StaffPage";
import { intakeColumns } from "./intake-columns";
import styles from "./IntakeWorklist.module.css";

interface IntakeWorklistProps {
  actor: CurrentActor;
  initial: ApiResult<Page<WorklistItem>>;
  filters: StaffFilters;
  page: number;
  branches: Page<Branch> | null;
  selectedId: string;
  selected: ApiResult<AppointmentDetail> | null;
}

export function IntakeWorklist(props: IntakeWorklistProps) {
  const [navigation, setNavigation] = useState({ snapshot: props.initial, revision: 0 });
  // A fresh server navigation owns a new workflow; in-page refreshes retain their drafts.
  if (navigation.snapshot !== props.initial) {
    setNavigation({ snapshot: props.initial, revision: navigation.revision + 1 });
  }
  return <Worklist key={navigation.revision} {...props} />;
}

function Worklist({
  actor,
  initial,
  filters,
  page,
  branches,
  selectedId,
  selected,
}: IntakeWorklistProps) {
  const worklist = useStaffWorklist(initial, filters, page, branches);
  useInitialToast(
    actor.role === "GeneralManager" && !branches
      ? "Branch choices are unavailable. All-branch access remains available."
      : null,
  );
  const drawer = useStaffAppointmentDrawer(selectedId, selected);
  const exportController = useRef<AbortController | null>(null);
  useEffect(() => () => exportController.current?.abort(), []);
  const disabled = Boolean(drawer.id);
  const { snapshot, active } = worklist;
  const [tableSort, setTableSort] = useState<TableSort | null>(() => {
    if (filters.sort === "startsAt" && filters.direction === "asc") {
      return null;
    }
    return { column: filters.sort, direction: filters.direction };
  });
  const query = staffViewQuery(active.filters, active.page);
  const columns = intakeColumns(query, drawer.open);
  const filterControls = (
    <WorklistFilters
      worklist={worklist}
      disabled={disabled}
      group={actor.role === "GeneralManager"}
    />
  );
  const branchName =
    worklist.branches?.items.find((branch) => branch.id === active.filters.branchId)?.name ??
    snapshot?.items.find((item) => item.branchId === active.filters.branchId)?.branchName ??
    "Selected branch";
  const appliedFilters = appliedWorklistFilters(active.filters, branchName);

  async function sortWorklist(sort: TableSort | null) {
    const confirmed = await worklist.load(
      {
        ...active.filters,
        sort: sort?.column ?? "startsAt",
        direction: sort?.direction ?? "asc",
      },
      1,
    );
    if (confirmed) {
      setTableSort(sort);
    }
  }

  async function exportAll() {
    exportController.current?.abort();
    const controller = new AbortController();
    exportController.current = controller;
    try {
      await exportWorklist(active.filters, actor, controller.signal);
    } catch (error) {
      if (!controller.signal.aborted) {
        throw error;
      }
    }
  }

  return (
    <>
      <div
        className={styles.page}
        inert={!worklist.ready || worklist.busy}
        aria-busy={worklist.busy}
      >
        <StaffCommandBar
          title="Intake appointments"
          actions={
            <>
              <RefreshButton
                label="Refresh worklist"
                busy={worklist.busy}
                disabled={disabled}
                onClick={() => void worklist.load()}
              />
              <IntakeViewNavigation
                actor={actorIdentity(actor)}
                active="list"
                destinations={{
                  calendar: calendarViewHref(active.filters),
                  list: `/dashboard/intake?${query}`,
                }}
              />
            </>
          }
        />
        {!snapshot && <div className={styles.unavailableFilters}>{filterControls}</div>}
        <div className={styles.body}>
          {snapshot && (
            <DataTable
              fillHeight
              label="Scoped intake appointments"
              rows={snapshot.items}
              columns={columns}
              rowId={(row) => row.id}
              page={{ ...snapshot, page: active.page }}
              sort={tableSort}
              disabled={disabled}
              busy={worklist.busy}
              selectionScope={`${actorIdentity(actor)}:${query}:${worklist.revision}`}
              selectedRowId={drawer.id}
              onRowClick={(row) => drawer.open(row.id)}
              onSort={(sort) => void sortWorklist(sort)}
              onPage={(nextPage, pageSize) =>
                void worklist.load({ ...active.filters, pageSize }, nextPage)
              }
              exportFileName="intake-appointments"
              onExportAll={exportAll}
              filters={filterControls}
              onFiltersOpenChange={(open) => {
                if (open) {
                  return;
                }
                worklist.setDraft((draft) => ({
                  ...draft,
                  from: active.filters.from,
                  to: active.filters.to,
                  status: active.filters.status,
                  branchId: active.filters.branchId,
                }));
              }}
              filterCount={appliedFilters.length}
              filterSummary={
                <AppliedWorklistFilters
                  filters={appliedFilters}
                  disabled={disabled}
                  onRemove={(filters) => void worklist.load(filters, 1)}
                />
              }
              toolbar={
                <form
                  className={styles.search}
                  onSubmit={(event) => {
                    event.preventDefault();
                    void worklist.load(
                      { ...active.filters, search: worklist.draft.search.trim() },
                      1,
                    );
                  }}
                >
                  <SearchField
                    label="Search appointments"
                    placeholder="Search appointments"
                    value={worklist.draft.search}
                    disabled={disabled}
                    onChange={(search) => worklist.setDraft({ ...worklist.draft, search })}
                    onClear={
                      active.filters.search
                        ? () => void worklist.load({ ...active.filters, search: "" }, 1)
                        : undefined
                    }
                  />
                </form>
              }
            />
          )}
        </div>
      </div>
      <IntakeAppointmentDrawer
        actor={actor}
        drawer={drawer}
        refreshing={worklist.busy}
        onConfirmed={() => void worklist.load()}
      />
    </>
  );
}
