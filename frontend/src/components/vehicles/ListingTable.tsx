"use client";

import { useContext } from "react";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { SearchField } from "@/components/ui/SearchField";
import { Button } from "@/components/ui/Button";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { CsvExportActions } from "@/components/ui/CsvExportActions";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { TableSort } from "@/components/ui/data-table/types";
import { useListingManager } from "@/hooks/use-listing-manager";
import { useRowSelection } from "@/hooks/use-row-selection";
import { useCsvExport } from "@/hooks/use-csv-export";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import {
  listingQuery,
  listingPublicationOptions,
  type ListingItem,
  type ListingSection,
  type ListingFilters as Filters,
} from "@/lib/vehicles/listings";
import { exportListings, listingExportColumns } from "@/lib/vehicles/listing-export";
import { listingTableFilters } from "./listing-table-filters";
import { ListingDrawer } from "./ListingDrawer";
import { listingColumns } from "./listing-columns";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import type { ListingRecordSelection } from "@/lib/vehicles/listing-history-selection";
import styles from "@/components/staff/StaffWorklist.module.css";
import workspace from "@/components/staff/StaffWorkspace.module.css";

const rowId = (row: ListingItem) => row.vehicleId;

export function ListingTable({
  actor,
  initial,
  filters,
  page,
  branches,
  listingId,
  selected,
  section,
  record = null,
}: {
  actor: CurrentActor;
  initial: ApiResult<Page<ListingItem>>;
  filters: Filters;
  page: number;
  branches: Page<Branch> | null;
  listingId: string;
  selected: ApiResult<ListingWorkspace> | null;
  section: ListingSection;
  record?: ListingRecordSelection | null;
}) {
  const {
    table: state,
    drawer,
    refresh,
  } = useListingManager({
    initial,
    filters,
    page,
    branches,
    listingId,
    selected,
    section,
    record,
  });
  const baseColumns = listingColumns(actor.role === "GeneralManager");
  const animateEntry = useContext(StaffPageEntranceContext);
  const { snapshot, active } = state;
  const selection = useRowSelection(
    snapshot?.items ?? [],
    rowId,
    `${actorIdentity(actor)}:${listingQuery(active.filters, active.page)}:${state.revision}`,
  );
  const csv = useCsvExport(listingExportColumns, "listings", (signal) =>
    exportListings(active.filters, signal),
  );
  const { columns, applied } = listingTableFilters(state, baseColumns);
  const refinements = applied.length > 0;
  const filtered = Boolean(active.filters.search || active.filters.publication || refinements);

  function resetFilters() {
    void state.load(
      {
        ...active.filters,
        branchId: "",
        inspection: "",
        minimumPrice: "",
        maximumPrice: "",
        minimumPublishedPrice: "",
        maximumPublishedPrice: "",
        from: "",
        to: "",
        registration: "",
        vehicle: "",
      },
      1,
      true,
    );
  }

  function changePage(nextPage: number, pageSize: number) {
    void state.load({ ...active.filters, pageSize }, nextPage);
  }

  function sortListings(sort: TableSort | null) {
    void state.load(
      { ...active.filters, sort: sort?.column ?? "", direction: sort?.direction ?? "asc" },
      1,
    );
  }

  return (
    <div className={styles.workspace}>
      <LoadingProgress label="Loading listings" active={state.busy} />
      <StaffCommandBar
        title="Listings"
        actions={
          <RefreshButton
            label="Refresh listings"
            busy={state.busy}
            disabled={!state.ready}
            onClick={() => void refresh()}
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
                  <StaffQueueTabs
                    label="Listing queues"
                    mobilePresentation="select"
                    countNoun="listings"
                    tabs={listingPublicationOptions.map((option) => ({
                      ...option,
                      count: snapshot.counts?.[option.value || "all"],
                    }))}
                    value={active.filters.publication ?? ""}
                    disabled={state.busy}
                    onChange={(publication) =>
                      void state.load({ ...active.filters, publication }, 1)
                    }
                  />
                  <RecordToolbar
                    filters={null}
                    filterCount={applied.length}
                    disabled={false}
                    filterSummary={
                      <AppliedWorklistFilters
                        filters={applied}
                        disabled={state.busy}
                        onRemove={(next) => void state.load(next, 1)}
                      />
                    }
                    actions={
                      <>
                        {refinements && (
                          <Button
                            size="compact"
                            variant="ghost"
                            disabled={state.busy}
                            onClick={resetFilters}
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
                        void state.load(
                          { ...active.filters, search: state.draft.search.trim() },
                          1,
                        );
                      }}
                    >
                      <SearchField
                        label="Search listings"
                        placeholder="Search vehicle, year, VIN or registration"
                        value={state.draft.search}
                        disabled={false}
                        busy={state.busy}
                        onChange={(search) => state.setDraft({ ...state.draft, search })}
                        onClear={
                          state.draft.search || active.filters.search
                            ? () => {
                                if (active.filters.search) {
                                  void state.load({ ...active.filters, search: "" }, 1);
                                  return;
                                }
                                state.setDraft((draft) => ({ ...draft, search: "" }));
                              }
                            : undefined
                        }
                      />
                    </form>
                  </RecordToolbar>
                </>
              }

              label="Listings"
              rows={snapshot.items}
              columns={columns}
              sorting={{
                value: active.filters.sort
                  ? { column: active.filters.sort, direction: active.filters.direction }
                  : null,
                onChange: sortListings,
              }}
              rowId={rowId}
              selection={selection.selection}
              page={snapshot}
              onPage={changePage}
              busy={state.busy}
              disabled={false}
              selectedRowId={drawer.pendingId || drawer.id}
              onRowClick={(row) => void drawer.choose(row.vehicleId)}
              emptyState={{
                title: filtered ? "No listings match your criteria" : "No vehicles to list yet",
                description: filtered
                  ? "Try another search or clear the filters."
                  : "Checked-in vehicles will appear here for listing preparation.",
                ...((refinements || active.filters.search) && {
                  action: {
                    label: refinements ? "Reset filters" : "Clear search",
                    onClick: () => {
                      if (refinements) {
                        resetFilters();
                        return;
                      }
                      void state.load({ ...active.filters, search: "" }, 1, true);
                    },
                  },
                }),
              }}
            />
          ) : (
            <div className={styles.unavailableFilters}>
              <p>Listings are unavailable. Retry when the service is available.</p>
              <Button size="compact" variant="secondary" onClick={() => void refresh()}>
                Retry listings
              </Button>
            </div>
          )}
        </div>
      </div>
      <ListingDrawer actor={actor} selection={drawer} onSaved={state.refreshAfterCommand} />
    </div>
  );
}
