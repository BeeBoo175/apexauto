"use client";

import { SearchField } from "@/components/ui/SearchField";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { CsvExportActions } from "@/components/ui/CsvExportActions";
import { ResetFiltersButton } from "@/components/ui/ResetFiltersButton";
import { DataTable } from "@/components/ui/data-table/DataTable";
import { TableDiscovery } from "@/components/ui/data-table/TableDiscovery";
import { useRowSelection } from "@/hooks/use-row-selection";
import { useCsvExport } from "@/hooks/use-csv-export";
import type { TableSort } from "@/components/ui/data-table/types";
import type { useStaffWorklist } from "@/hooks/use-staff-worklist";
import { useToast } from "@/components/ui/Toast";
import type { IntakeDrawer } from "@/hooks/use-intake-history";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { staffViewQuery } from "@/lib/staff/query";
import { exportWorklist } from "@/lib/staff/export";
import { intakeExportColumns } from "@/lib/staff/export-columns";
import { appliedIntakeFilters, resetWorklistFilters } from "@/lib/staff/filter-summary";
import { StaffQueueTabs } from "./StaffQueueTabs";
import { AppliedWorklistFilters } from "./AppliedWorklistFilters";
import { WorklistFilters } from "./WorklistFilters";
import { intakeColumnFilters } from "./intake-column-filters";
import { intakeColumns } from "./intake-columns";
import styles from "./StaffWorklist.module.css";
import { AssistedIntakeAction } from "./AssistedIntakeAction";

interface IntakeWorklistProps {
  actor: CurrentActor;
  worklist: ReturnType<typeof useStaffWorklist>;
  drawer: IntakeDrawer;
}

const intakeSections = [
  { value: "all", label: "All appointments", count: "all" },
  { value: "booked", label: "Booked", count: "booked" },
  { value: "checkedIn", label: "Checked in", count: "checkedIn" },
  { value: "noShow", label: "No shows", count: "noShow" },
  { value: "cancelled", label: "Cancelled", count: "cancelled" },
] as const;

export function IntakeWorklist({ actor, worklist, drawer }: IntakeWorklistProps) {
  const toast = useToast();
  const disabled = Boolean(drawer.id);
  const { snapshot, active } = worklist;
  const query = staffViewQuery(active.filters, active.page);
  const columns = intakeColumnFilters(
    worklist,
    intakeColumns(actor.role === "GeneralManager"),
    actor.role === "GeneralManager",
    disabled,
  );
  const selection = useRowSelection(
    snapshot?.items ?? [],
    (row) => row.id,
    `${actorIdentity(actor)}:${query}:${worklist.revision}`,
  );
  const sorting = {
    value: active.sort,
    onChange: (sort: TableSort | null) => void sortWorklist(sort),
  };
  const csv = useCsvExport(intakeExportColumns, "intake-appointments", (signal) =>
    exportWorklist(active.filters, actor, signal, toast.info),
  );
  function clearFilters() {
    void worklist.load(
      {
        ...resetWorklistFilters(active.filters),
        search: active.filters.search,
        status: active.filters.status,
      },
      1,
    );
  }

  const filterControls = (
    <WorklistFilters
      worklist={worklist}
      disabled={disabled || worklist.busy}
      group={actor.role === "GeneralManager"}
    />
  );
  const branchName =
    worklist.branches?.items.find((branch) => branch.id === active.filters.branchId)?.name ??
    snapshot?.items.find((item) => item.branchId === active.filters.branchId)?.branchName ??
    "Selected branch";
  const appliedFilters = appliedIntakeFilters(active.filters, branchName);
  // The section tab is not a criterion to clear, so an empty tab never offers a clear.
  const narrowed = appliedFilters.length > 0 || Boolean(active.filters.search);

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
    <div className={styles.page} inert={!worklist.ready} aria-busy={worklist.busy}>
      {!snapshot && <div className={styles.unavailableFilters}>{filterControls}</div>}
      <div className={styles.body}>
        {snapshot && (
          <DataTable
            toolbar={
              <>
                <StaffQueueTabs
                  label="Intake sections"
                  countNoun="appointments"
                  tabs={intakeSections.map((section) => ({
                    value: section.value,
                    label: section.label,
                    count: snapshot.counts?.[section.count],
                  }))}
                  value={active.filters.status}
                  disabled={disabled || worklist.busy}
                  onChange={(status) => void worklist.load({ ...active.filters, status }, 1)}
                />
                <RecordToolbar
                  filters={null}
                  mobileDiscovery={
                    <TableDiscovery
                      label="intake appointments"
                      columns={columns}
                      sorting={sorting}
                      filterCount={appliedFilters.length}
                      disabled={disabled || worklist.busy}
                      onClearFilters={clearFilters}
                    />
                  }
                  disabled={disabled}
                  filterCount={appliedFilters.length}
                  filterSummary={
                    <AppliedWorklistFilters
                      filters={appliedFilters}
                      disabled={disabled || worklist.busy}
                      onRemove={(filters) => void worklist.load(filters, 1)}
                    />
                  }
                  actions={
                    <>
                      {appliedFilters.length > 0 && (
                        <ResetFiltersButton
                          disabled={disabled || worklist.busy}
                          onClick={clearFilters}
                        />
                      )}
                      <CsvExportActions
                        create={
                          <AssistedIntakeAction
                            open={drawer.creating}
                            onOpen={drawer.openCreate}
                            onClose={drawer.closeCreate}
                            disabled={disabled || worklist.busy}
                            onBooked={() => void worklist.load(active.filters, active.page)}
                          />
                        }
                        selected={selection.selectedRows.length}
                        disabled={disabled || worklist.busy || snapshot.total === 0}
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
                      if (worklist.busy) {
                        return;
                      }
                      void worklist.load(
                        { ...active.filters, search: worklist.draft.search.trim() },
                        1,
                      );
                    }}
                  >
                    <SearchField
                      label="Search appointments"
                      placeholder="Search vehicle, customer or contact"
                      value={worklist.draft.search}
                      disabled={disabled}
                      busy={worklist.busy}
                      onChange={(search) => worklist.setDraft({ ...worklist.draft, search })}
                      onClear={
                        active.filters.search
                          ? () => void worklist.load({ ...active.filters, search: "" }, 1)
                          : undefined
                      }
                    />
                  </form>
                </RecordToolbar>
              </>
            }

            label="Scoped intake appointments"
            rows={snapshot.items}
            emptyState={{
              noun: "appointments",
              icon: "calendar",
              narrowed,
              hint: "Booked appointments appear here when customers schedule an inspection.",
              onClear: () =>
                void worklist.load(
                  {
                    ...resetWorklistFilters(active.filters),
                    status: active.filters.status,
                  },
                  1,
                ),
            }}
            columns={columns}
            rowId={(row) => row.id}
            page={{ ...snapshot, page: active.page }}
            mobile="columns"
            sorting={sorting}
            disabled={disabled}
            busy={worklist.busy}
            selection={selection.selection}
            selectedRowId={drawer.id}
            onRowClick={(row) => drawer.open(row.id)}
            onPage={changePage}
          />
        )}
      </div>
    </div>
  );
}
