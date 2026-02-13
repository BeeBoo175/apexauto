"use client";

import { useEffect, useRef } from "react";
import { SearchField } from "@/components/ui/SearchField";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { TableSort } from "@/components/ui/data-table/types";
import type { useStaffWorklist } from "@/hooks/use-staff-worklist";
import { useToast } from "@/components/ui/Toast";
import type { useStaffAppointmentDrawer } from "@/hooks/use-staff-appointment-drawer";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { staffViewQuery } from "@/lib/staff/query";
import { exportWorklist } from "@/lib/staff/export";
import { appliedWorklistFilters } from "@/lib/staff/filter-summary";
import { AppliedWorklistFilters } from "./AppliedWorklistFilters";
import { WorklistFilters } from "./WorklistFilters";
import { intakeColumns } from "./intake-columns";
import styles from "./IntakeWorklist.module.css";

interface IntakeWorklistProps {
  actor: CurrentActor;
  worklist: ReturnType<typeof useStaffWorklist>;
  drawer: ReturnType<typeof useStaffAppointmentDrawer>;
}

export function IntakeWorklist({ actor, worklist, drawer }: IntakeWorklistProps) {
  const exportController = useRef<AbortController | null>(null);
  const toast = useToast();
  useEffect(() => () => exportController.current?.abort(), []);
  const disabled = Boolean(drawer.id);
  const { snapshot, active } = worklist;
  const filters = active.filters;
  const tableSort: TableSort | null =
    filters.sort === "startsAt" && filters.direction === "asc"
      ? null
      : { column: filters.sort, direction: filters.direction };
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
    await worklist.load(
      {
        ...active.filters,
        sort: sort?.column ?? "startsAt",
        direction: sort?.direction ?? "asc",
      },
      1,
    );
  }

  async function exportAll() {
    exportController.current?.abort();
    const controller = new AbortController();
    exportController.current = controller;
    try {
      await exportWorklist(active.filters, actor, controller.signal, toast.info);
    } catch (error) {
      if (!controller.signal.aborted) {
        throw error;
      }
    }
  }

  return (
    <div className={styles.page} inert={!worklist.ready || worklist.busy} aria-busy={worklist.busy}>
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
  );
}
