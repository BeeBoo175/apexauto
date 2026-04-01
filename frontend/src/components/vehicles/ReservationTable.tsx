"use client";

import { useContext } from "react";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { Button } from "@/components/ui/Button";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { CsvExportActions } from "@/components/ui/CsvExportActions";
import { DataTable } from "@/components/ui/data-table/DataTable";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import { DataTableWorkspace } from "@/components/ui/data-table/DataTableWorkspace";
import type { DataColumn, TableSort } from "@/components/ui/data-table/types";
import { useReservationTable } from "@/hooks/use-reservation-table";
import { useRowSelection } from "@/hooks/use-row-selection";
import { useCsvExport } from "@/hooks/use-csv-export";
import { useStaffSelection } from "@/hooks/use-staff-selection";
import { apiFetch } from "@/lib/api-browser";
import {
  parseReservationDetailSnapshot,
  type ReservationDetail,
} from "@/lib/vehicles/reservation-detail";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import {
  reservationQuery,
  reservationStatuses,
  type ReservationItem,
  type ReservationFilters as Filters,
} from "@/lib/vehicles/reservations";
import { exportReservations, reservationExportColumns } from "@/lib/vehicles/reservation-export";
import type { AppliedWorklistFilter } from "@/lib/staff/filter-summary";
import { ReservationFilters } from "./ReservationFilters";
import { ReservationDrawer } from "./ReservationDrawer";
import layout from "@/components/staff/IntakeWorkspace.module.css";
import styles from "@/components/staff/IntakeWorklist.module.css";
import workspace from "@/components/staff/StaffWorkspace.module.css";

const rowId = (row: ReservationItem) => row.id;
const columns: DataColumn<ReservationItem>[] = [
  { id: "reference", label: "Reference", sortable: true, minWidth: 190, cell: rowId },
  { id: "vehicle", label: "Vehicle", sortable: true, minWidth: 230, cell: (row) => row.vehicle },
  { id: "buyer", label: "Buyer", sortable: true, minWidth: 180, cell: (row) => row.buyer },
  { id: "branch", label: "Branch", sortable: true, minWidth: 160, cell: (row) => row.branchName },
  {
    id: "created",
    label: "Created",
    sortable: true,
    minWidth: 200,
    cell: (row) => formatMalaysianDateTime(row.createdAt),
  },
  {
    id: "expires",
    label: "Expires",
    sortable: true,
    minWidth: 200,
    cell: (row) => formatMalaysianDateTime(row.expiresAt),
  },
  {
    id: "total",
    label: "Reserved total",
    sortable: true,
    minWidth: 160,
    align: "right",
    cell: (row) => formatCurrency(row.total),
  },
  {
    id: "status",
    label: "Status",
    sortable: true,
    minWidth: 175,
    cell: (row) => reservationStatuses[row.status],
  },
];

export function ReservationTable({
  actor,
  initial,
  filters,
  page,
  branches,
  reservationId,
  selected,
}: {
  actor: CurrentActor;
  initial: ApiResult<Page<ReservationItem>>;
  filters: Filters;
  page: number;
  branches: Page<Branch> | null;
  reservationId: string;
  selected: ApiResult<ReservationDetail> | null;
}) {
  const state = useReservationTable(initial, filters, page, branches);
  const drawer = useStaffSelection(reservationId, selected, {
    parameter: "reservation",
    path: (id) => `/api/staff/reservations/${id}`,
    parse: parseReservationDetailSnapshot,
    matches: (value, id) => value.reservation.id === id,
    read: (path, parse, options) => apiFetch(path, parse, options, { requireActor: true }),
    commitAfterRead: true,
  });
  const reservationColumns = columns.map((column) =>
    column.id === "reference"
      ? {
          ...column,
          cell: (row: ReservationItem) => (
            <Button size="compact" variant="ghost" onClick={() => void drawer.choose(row.id)}>
              {row.id}
            </Button>
          ),
        }
      : column,
  );
  const animateEntry = useContext(StaffPageEntranceContext);
  const { snapshot, active } = state;
  const selectedView = state.pendingView ?? active.filters.isActive;
  const pendingLabel = state.pendingView ? "Opening active holds" : "Opening reservation history";
  const selection = useRowSelection(
    snapshot?.items ?? [],
    rowId,
    `${actorIdentity(actor)}:${reservationQuery(active.filters, active.page)}:${state.revision}`,
  );
  const csv = useCsvExport(reservationExportColumns, "reservations", (signal) =>
    exportReservations(active.filters, signal),
  );
  const applied: AppliedWorklistFilter<Filters>[] = active.filters.branchId
    ? [
        {
          key: "branch",
          label:
            state.branches?.items.find((branch) => branch.id === active.filters.branchId)?.name ??
            "Selected branch",
          without: { ...active.filters, branchId: "" },
        },
      ]
    : [];
  const filterControls = (
    <ReservationFilters state={state} group={actor.role === "GeneralManager"} />
  );

  function changePage(nextPage: number, pageSize: number) {
    void state.load({ ...active.filters, pageSize }, nextPage);
  }

  function sortReservations(sort: TableSort | null) {
    void state.load(
      { ...active.filters, sort: sort?.column ?? "", direction: sort?.direction ?? "asc" },
      1,
    );
  }

  return (
    <div className={layout.workspace}>
      <LoadingProgress label="Loading reservations" active={state.busy} />
      <StaffCommandBar
        title="Reservations"
        actions={
          <RefreshButton
            label="Refresh reservations"
            busy={state.busy && state.pendingView === null}
            disabled={!state.ready || state.pendingView !== null}
            onClick={() => void state.load()}
          />
        }
      />
      <div
        className={`${styles.page} ${animateEntry ? workspace.reveal : ""}`}
        aria-busy={state.busy}
        inert={!state.ready}
      >
        <div className={styles.body}>
          {snapshot ? (
            <DataTableWorkspace label="Reservations">
              <RecordToolbar
                filters={filterControls}
                filterCount={applied.length}
                disabled={state.busy}
                onFiltersOpenChange={(open) => {
                  if (!open) {
                    state.setDraft((draft) => ({ ...draft, branchId: active.filters.branchId }));
                  }
                }}
                filterSummary={
                  <AppliedWorklistFilters
                    filters={applied}
                    disabled={state.busy}
                    onRemove={(next) => void state.load(next, 1)}
                  />
                }
                actions={
                  <CsvExportActions
                    selected={selection.selectedRows.length}
                    disabled={state.busy || snapshot.total === 0}
                    busy={csv.busy}
                    onClear={selection.clear}
                    onSelected={() => csv.exportSelected(selection.selectedRows)}
                    onAll={() => void csv.exportAll()}
                  />
                }
              >
                <div
                  role="group"
                  aria-label="Reservation views"
                  aria-busy={state.pendingView !== null}
                >
                  <span className="sr-only" role="status">
                    {state.pendingView !== null && pendingLabel}
                  </span>
                  {[true, false].map((isActive) => (
                    <Button
                      key={String(isActive)}
                      size="compact"
                      variant={selectedView === isActive ? "secondary" : "ghost"}
                      aria-pressed={selectedView === isActive}
                      disabled={state.busy}
                      onClick={() => void state.load({ ...active.filters, isActive }, 1)}
                    >
                      {isActive ? "Active holds" : "History"}
                    </Button>
                  ))}
                </div>
              </RecordToolbar>
              <DataTable
                label="Reservations"
                rows={snapshot.items}
                columns={reservationColumns}
                sorting={{
                  value: active.filters.sort
                    ? { column: active.filters.sort, direction: active.filters.direction }
                    : null,
                  onChange: sortReservations,
                }}
                rowId={rowId}
                selection={selection.selection}
                page={snapshot}
                onPage={changePage}
                busy={state.busy}
                disabled={false}
                selectedRowId={drawer.id}
                onRowClick={(row) => void drawer.choose(row.id)}
                emptyState={{
                  title: active.filters.isActive ? "No active holds" : "No reservation history",
                  description: active.filters.isActive
                    ? "Confirmed active reservations will appear here."
                    : "Released reservations will appear here.",
                }}
              />
              <DataTablePagination
                page={snapshot}
                count={snapshot.items.length}
                disabled={state.busy}
                onPage={changePage}
              />
            </DataTableWorkspace>
          ) : (
            <div className={styles.unavailableFilters}>
              <p>Reservations are unavailable. Retry when the service is available.</p>
              <Button size="compact" variant="secondary" onClick={() => void state.load()}>
                Retry reservations
              </Button>
              {filterControls}
            </div>
          )}
        </div>
      </div>
      <ReservationDrawer
        key={`${actorIdentity(actor)}:${drawer.id}`}
        actor={actor}
        selection={drawer}
        onConfirmed={() => {
          void state.refresh();
          void drawer.choose(drawer.id);
        }}
      />
    </div>
  );
}
