"use client";

import { useContext } from "react";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { SearchField } from "@/components/ui/SearchField";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { Button } from "@/components/ui/Button";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { CsvExportActions } from "@/components/ui/CsvExportActions";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { TableSort } from "@/components/ui/data-table/types";
import { useReservationTable } from "@/hooks/use-reservation-table";
import { useRowSelection } from "@/hooks/use-row-selection";
import { useCsvExport } from "@/hooks/use-csv-export";
import { useStaffSelection } from "@/hooks/use-staff-selection";
import { privateApiFetch } from "@/lib/api-browser";
import {
  parseReservationDetailSnapshot,
  type ReservationDetail,
} from "@/lib/vehicles/reservation-detail";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import {
  reservationQuery,
  reservationFilterDefaults,
  reservationView,
  reservationViewFilters,
  type ReservationItem,
  type ReservationPage,
  type ReservationFilters as Filters,
} from "@/lib/vehicles/reservations";
import { exportReservations, reservationExportColumns } from "@/lib/vehicles/reservation-export";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { reservationColumns } from "./reservation-columns";
import { reservationTableFilters } from "./reservation-table-filters";
import { ReservationDrawer } from "./ReservationDrawer";
import layout from "@/components/staff/IntakeWorkspace.module.css";
import styles from "@/components/staff/IntakeWorklist.module.css";
import workspace from "@/components/staff/StaffWorkspace.module.css";

const rowId = (row: ReservationItem) => row.id;

const viewLabels = {
  open: "open holds",
  handled: "holds being handled",
  history: "reservation history",
};

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
  initial: ApiResult<ReservationPage>;
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
    read: privateApiFetch,
    commitAfterRead: true,
  });
  const animateEntry = useContext(StaffPageEntranceContext);
  const { snapshot, active } = state;
  const view = reservationView(active.filters);
  const { columns, applied } = reservationTableFilters(
    state,
    reservationColumns(actor.role === "GeneralManager", view, state.fetchedAt),
  );
  const selectedView = state.pendingView ?? view;
  const selection = useRowSelection(
    snapshot?.items ?? [],
    rowId,
    `${actorIdentity(actor)}:${reservationQuery(active.filters, active.page)}:${state.revision}`,
  );
  const csv = useCsvExport(reservationExportColumns, "reservations", (signal) =>
    exportReservations(active.filters, signal),
  );
  const filtered = applied.length > 0 || Boolean(active.filters.search);
  let emptyTitle = `No ${viewLabels[view]}`;
  if (filtered) {
    emptyTitle = "No reservations match your criteria";
  }

  function resetFilters(clearSearch = false) {
    void state.load(
      {
        ...active.filters,
        ...reservationFilterDefaults,
        branchId: "",
        search: clearSearch ? "" : active.filters.search,
      },
      1,
    );
  }

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
            <DataTable
              toolbar={
                <>
                  <span className="sr-only" role="status">
                    {state.pendingView !== null && `Opening ${viewLabels[state.pendingView]}`}
                  </span>
                  <StaffQueueTabs
                    label="Reservation views"
                    countNoun="reservations"
                    tabs={[
                      { value: "open", label: "Open", count: snapshot.counts?.open },
                      { value: "handled", label: "Being handled", count: snapshot.counts?.handled },
                      { value: "history", label: "History", count: snapshot.counts?.history },
                    ]}
                    value={selectedView}
                    disabled={state.busy}
                    onChange={(next) =>
                      void state.load(reservationViewFilters(active.filters, next), 1)
                    }
                  />
                  <RecordToolbar
                    filters={null}
                    filterCount={applied.length}
                    disabled={state.busy}
                    filterSummary={
                      <AppliedWorklistFilters
                        filters={applied}
                        disabled={state.busy}
                        onRemove={(next) => void state.load(next, 1)}
                      />
                    }
                    actions={
                      <>
                        {applied.length > 0 && (
                          <Button
                            size="compact"
                            variant="ghost"
                            disabled={state.busy}
                            onClick={() => resetFilters()}
                          >
                            Reset filters
                          </Button>
                        )}
                        <CsvExportActions
                          selected={selection.selectedRows.length}
                          disabled={state.busy || snapshot.total === 0}
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
                        if (!state.busy) {
                          void state.load(
                            { ...active.filters, search: state.draft.search?.trim() ?? "" },
                            1,
                          );
                        }
                      }}
                    >
                      <SearchField
                        label="Search reservations"
                        placeholder="Search vehicle, VIN, buyer, contact, promo or agent"
                        value={state.draft.search ?? ""}
                        disabled={false}
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

              label="Reservations"
              rows={snapshot.items}
              columns={columns}
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
                title: emptyTitle,
                description:
                  "Search a vehicle, buyer, contact, promo or agent, or adjust the column filters.",
                ...(filtered && {
                  action: {
                    label: "Clear search and filters",
                    onClick: () => resetFilters(true),
                  },
                }),
              }}
            />
          ) : (
            <div className={styles.unavailableFilters}>
              <p>Reservations are unavailable. Retry when the service is available.</p>
              <Button size="compact" variant="secondary" onClick={() => void state.load()}>
                Retry reservations
              </Button>
            </div>
          )}
        </div>
      </div>
      <ReservationDrawer
        key={`${actorIdentity(actor)}:${drawer.id}`}
        actor={actor}
        now={state.fetchedAt}
        selection={drawer}
        onConfirmed={() => {
          void state.refresh();
          void drawer.choose(drawer.id);
        }}
      />
    </div>
  );
}
