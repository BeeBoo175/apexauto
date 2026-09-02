"use client";
import { useContext, type ReactNode } from "react";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { DataTable } from "@/components/ui/data-table/DataTable";
import { TableDiscovery } from "@/components/ui/data-table/TableDiscovery";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { CsvExportActions } from "@/components/ui/CsvExportActions";
import { ResetFiltersButton } from "@/components/ui/ResetFiltersButton";
import { useRowSelection } from "@/hooks/use-row-selection";
import { useCsvExport } from "@/hooks/use-csv-export";
import type { DataColumn, TableSort } from "@/components/ui/data-table/types";
import type { CsvColumn } from "@/lib/csv";
import { SearchField } from "@/components/ui/SearchField";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { appliedWorklistFilters, type AppliedFilter } from "@/lib/staff/filter-summary";
import type { QueueFilters as Filters } from "@/lib/inspection/contracts";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { Page } from "@/lib/api";
import { exportQueue } from "@/lib/inspection/export";
import { queueQuery } from "@/lib/inspection/query";
import {
  workshopDate,
  workshopTasks,
  workshopTaskLabel,
  workshopStatuses,
  workshopStatusLabel,
} from "@/lib/inspection/workshop-contracts";
import type { useInspectionQueue } from "@/hooks/use-inspection-queue";
import { workshopColumnFilters } from "./workshop-column-filters";
import styles from "@/components/staff/StaffWorklist.module.css";
import workspace from "@/components/staff/StaffWorkspace.module.css";
import { moneyRangeLabel } from "@/lib/range-label";

export function QueueView<T>({
  actor,
  state,
  columns,
  exportColumns,
  rowId,
  onOpen,
  selectedRowId,
  parse,
  children,
  disabled = false,
}: {
  actor: CurrentActor;
  state: ReturnType<typeof useInspectionQueue<T>>;
  columns: DataColumn<T>[];
  exportColumns: readonly CsvColumn<T>[];
  rowId: (row: T) => string;
  onOpen: (row: T) => void;
  selectedRowId?: string;
  parse: (value: unknown) => Page<T> | null;
  children?: ReactNode;
  disabled?: boolean;
}) {
  const animateEntry = useContext(StaffPageEntranceContext);
  const kind = "workshop";
  const title = "Workshop";
  const { snapshot, active } = state;
  const selection = useRowSelection(
    snapshot?.items ?? [],
    rowId,
    `${actorIdentity(actor)}:${queueQuery(active.filters, active.page)}:${state.revision}`,
  );
  const csv = useCsvExport(exportColumns, kind, (signal) =>
    exportQueue(kind, active.filters, exportColumns, parse, rowId, signal),
  );
  const branchName =
    state.branches?.items.find((branch) => branch.id === active.filters.branchId)?.name ??
    "Selected branch";
  const applied: AppliedFilter<Filters>[] = appliedWorklistFilters(
    active.filters,
    branchName,
    workshopTaskLabel,
  ).filter((filter) => filter.key !== "status");
  if (active.filters.workStatus) {
    const status = workshopStatuses.find((value) => value === active.filters.workStatus);
    applied.push({
      key: "workStatus",
      label: status ? workshopStatusLabel(status) : active.filters.workStatus,
      without: { ...active.filters, workStatus: "" },
    });
  }
  for (const key of ["registration", "vehicle"] as const) {
    if (active.filters[key]) {
      applied.push({
        key,
        label: `${key === "registration" ? "Registration" : "Vehicle"}: ${active.filters[key]}`,
        without: { ...active.filters, [key]: "" },
      });
    }
  }
  if (active.filters.handler) {
    applied.push({
      key: "technician",
      label:
        active.filters.handler === "unassigned"
          ? "Unassigned"
          : `Technician: ${active.filters.handler}`,
      without: { ...active.filters, handler: "" },
    });
  }
  if (active.filters.minimumBudget || active.filters.maximumBudget) {
    applied.push({
      key: "budget",
      label: `Budget: ${moneyRangeLabel(active.filters.minimumBudget, active.filters.maximumBudget)}`,
      without: { ...active.filters, minimumBudget: "", maximumBudget: "" },
    });
  }
  const refinements =
    applied.length > 0 ||
    Boolean(
      active.filters.workStatus ||
      active.filters.handler ||
      active.filters.minimumBudget ||
      active.filters.maximumBudget,
    );
  // The status tab is not a criterion to clear, so an empty tab never offers a clear.
  const narrowed = refinements || Boolean(active.filters.search);

  function changePage(page: number, pageSize: number) {
    void state.load({ ...active.filters, pageSize }, page);
  }

  function sortQueue(sort: TableSort | null) {
    void state.load(
      {
        ...active.filters,
        sort: sort?.column ?? workshopDate(active.filters.status).column,
        direction: sort?.direction ?? "asc",
      },
      1,
      sort,
    );
  }

  const filteredColumns = workshopColumnFilters(state, columns, disabled);
  const sorting = { value: active.sort, onChange: sortQueue };

  function resetFilters(clearSearch = false) {
    void state.load(
      {
        ...active.filters,
        search: clearSearch ? "" : active.filters.search,
        branchId: "",
        from: "",
        to: "",
        workStatus: "",
        handler: "",
        minimumBudget: "",
        maximumBudget: "",
        registration: "",
        vehicle: "",
      },
      1,
    );
  }
  return (
    <div className={styles.workspace}>
      <LoadingProgress label={`Loading ${title.toLowerCase()}`} active={state.busy} />
      <StaffCommandBar
        title={title}
        actions={
          <RefreshButton
            label={`Refresh ${title.toLowerCase()}`}
            busy={state.busy}
            disabled={!state.ready || disabled}
            onClick={() => void state.refresh()}
          />
        }
      />
      <div
        className={`${styles.page} ${animateEntry ? workspace.reveal : ""}`}
        aria-busy={state.busy}
        inert={!state.ready || disabled}
      >
        <div className={styles.body}>
          {snapshot && (
            <DataTable
              toolbar={
                <>
                  <StaffQueueTabs
                    label="Workshop queues"
                    countNoun="vehicles"
                    tabs={workshopTasks.map((value) => ({
                      value,
                      label: workshopTaskLabel(value),
                      count: snapshot.counts?.[value.toLowerCase()],
                    }))}
                    value={active.filters.status}
                    disabled={disabled || state.busy}
                    onChange={(status) =>
                      void state.load(
                        {
                          ...active.filters,
                          status,
                          sort: workshopDate(status).column,
                          direction: "asc",
                        },
                        1,
                      )
                    }
                  />
                  <RecordToolbar
                    filters={null}
                    mobileDiscovery={
                      <TableDiscovery
                        label={title.toLowerCase()}
                        columns={filteredColumns}
                        sorting={sorting}
                        filterCount={applied.length}
                        disabled={disabled || state.busy}
                        onClearFilters={() => resetFilters()}
                      />
                    }
                    disabled={disabled}
                    filterCount={applied.length}
                    filterSummary={
                      <AppliedWorklistFilters
                        filters={applied}
                        disabled={disabled || state.busy}
                        onRemove={(filters) => void state.load(filters, 1)}
                      />
                    }
                    actions={
                      <>
                        {refinements && (
                          <ResetFiltersButton
                            disabled={disabled || state.busy}
                            onClick={() => resetFilters()}
                          />
                        )}
                        <CsvExportActions
                          selected={selection.selectedRows.length}
                          disabled={disabled || state.busy || snapshot.total === 0}
                          busy={csv.busy}
                          onClear={selection.clear}
                          onSelected={() => csv.exportSelected(selection.selectedRows)}
                          onAll={() => void csv.exportAll()}
                        />
                      </>
                    }
                  >
                    <form
                      className={styles.search}
                      onSubmit={(event) => {
                        event.preventDefault();
                        if (state.busy) {
                          return;
                        }
                        void state.load(
                          { ...active.filters, search: state.draft.search.trim() },
                          1,
                        );
                      }}
                    >
                      <SearchField
                        label={`Search ${title.toLowerCase()}`}
                        placeholder="Search vehicle or registration"
                        value={state.draft.search}
                        disabled={disabled}
                        busy={state.busy}
                        onChange={(search) => state.setDraft({ ...state.draft, search })}
                        onClear={
                          active.filters.search
                            ? () => void state.load({ ...active.filters, search: "" }, 1)
                            : undefined
                        }
                      />
                    </form>
                  </RecordToolbar>
                </>
              }

              label={title}
              rows={snapshot.items}
              emptyState={{
                noun: "workshop vehicles",
                icon: "tool",
                narrowed,
                hint: "Checked-in vehicles and their workshop work appear here.",
                onClear: () => resetFilters(true),
              }}
              columns={filteredColumns}
              mobile="columns"
              sorting={sorting}
              rowId={rowId}
              selection={selection.selection}
              selectedRowId={selectedRowId}
              onRowClick={onOpen}
              page={snapshot}
              onPage={changePage}
              disabled={disabled}
              busy={state.busy}
            />
          )}
          {!snapshot && (
            <div className={styles.unavailableFilters}>
              <ReloadNotice subject="Queue" />
            </div>
          )}
        </div>
      </div>
      {children}
    </div>
  );
}
