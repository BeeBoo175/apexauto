"use client";

import { SearchField } from "@/components/ui/SearchField";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { CsvExportActions } from "@/components/ui/CsvExportActions";
import { DataTable } from "@/components/ui/data-table/DataTable";
import { DataTableWorkspace } from "@/components/ui/data-table/DataTableWorkspace";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import { useRowSelection } from "@/hooks/use-row-selection";
import { useCsvExport } from "@/hooks/use-csv-export";
import type { TableSort } from "@/components/ui/data-table/types";
import type { useStaffWorklist } from "@/hooks/use-staff-worklist";
import { useToast } from "@/components/ui/Toast";
import type { useStaffAppointmentDrawer } from "@/hooks/use-staff-appointment-drawer";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { staffViewQuery } from "@/lib/staff/query";
import { exportWorklist } from "@/lib/staff/export";
import { intakeExportColumns } from "@/lib/staff/export-columns";
import { appliedWorklistFilters, resetWorklistFilters } from "@/lib/staff/filter-summary";
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
  const toast = useToast();
  const disabled = Boolean(drawer.id);
  const { snapshot, active } = worklist;
  const query = staffViewQuery(active.filters, active.page);
  const columns = intakeColumns(query, drawer.open);
  const selection = useRowSelection(
    snapshot?.items ?? [],
    (row) => row.id,
    `${actorIdentity(actor)}:${query}:${worklist.revision}`,
  );
  const csv = useCsvExport(intakeExportColumns, "intake-appointments", (signal) =>
    exportWorklist(active.filters, actor, signal, toast.info),
  );
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
  const filtered = appliedFilters.length > 0 || Boolean(active.filters.search);

  async function sortWorklist(sort: TableSort | null) {
    await worklist.load(
      {
        ...active.filters,
        sort: sort?.column ?? "startsAt",
        direction: sort?.direction ?? "asc",
      },
      1,
      true,
      sort,
    );
  }

  function changePage(page: number, pageSize: number) {
    void worklist.load({ ...active.filters, pageSize }, page);
  }

  return (
    <div className={styles.page} inert={!worklist.ready || worklist.busy} aria-busy={worklist.busy}>
      {!snapshot && <div className={styles.unavailableFilters}>{filterControls}</div>}
      <div className={styles.body}>
        {snapshot && (
          <DataTableWorkspace label="Scoped intake appointments">
            <RecordToolbar
              filters={filterControls}
              disabled={disabled}
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
              actions={
                <CsvExportActions
                  selected={selection.selectedRows.length}
                  disabled={disabled || worklist.busy || snapshot.total === 0}
                  busy={csv.busy}
                  onClear={selection.clear}
                  onSelected={() => csv.exportSelected(selection.selectedRows)}
                  onAll={() => void csv.exportAll()}
                />
              }
            >
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
            </RecordToolbar>
            <DataTable
              label="Scoped intake appointments"
              rows={snapshot.items}
              emptyState={{
                title: filtered
                  ? "No appointments match your criteria"
                  : "No intake appointments yet",
                description: filtered
                  ? "Try another search or clear the filters to see more appointments."
                  : "Booked appointments will appear here when customers schedule an inspection.",
                ...(filtered && {
                  action: {
                    label: "Clear search and filters",
                    onClick: () => void worklist.load(resetWorklistFilters(active.filters), 1),
                  },
                }),
              }}
              columns={columns}
              rowId={(row) => row.id}
              page={{ ...snapshot, page: active.page }}
              sorting={{ value: active.sort, onChange: (sort) => void sortWorklist(sort) }}
              disabled={disabled}
              busy={worklist.busy}
              selection={selection.selection}
              selectedRowId={drawer.id}
              onRowClick={(row) => drawer.open(row.id)}
              onPage={changePage}
            />
            <DataTablePagination
              page={{ ...snapshot, page: active.page }}
              count={snapshot.items.length}
              disabled={disabled}
              onPage={changePage}
            />
          </DataTableWorkspace>
        )}
      </div>
    </div>
  );
}
