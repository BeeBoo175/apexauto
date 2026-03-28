"use client";

import { useContext } from "react";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { SearchField } from "@/components/ui/SearchField";
import { Button } from "@/components/ui/Button";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { CsvExportActions } from "@/components/ui/CsvExportActions";
import { DataTable } from "@/components/ui/data-table/DataTable";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import { DataTableWorkspace } from "@/components/ui/data-table/DataTableWorkspace";
import type { DataColumn, TableSort } from "@/components/ui/data-table/types";
import { useListingTable } from "@/hooks/use-listing-table";
import { useRowSelection } from "@/hooks/use-row-selection";
import { useCsvExport } from "@/hooks/use-csv-export";
import { useStaffSelection } from "@/hooks/use-staff-selection";
import { apiFetch } from "@/lib/api-browser";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import { formatCurrency } from "@/lib/currency-format";
import {
  listingQuery,
  listingPublication,
  type ListingItem,
  type ListingFilters as Filters,
} from "@/lib/vehicles/listings";
import { exportListings, listingExportColumns } from "@/lib/vehicles/listing-export";
import type { AppliedWorklistFilter } from "@/lib/staff/filter-summary";
import { ListingFilters } from "./ListingFilters";
import { ListingDrawer } from "./ListingDrawer";
import { parseListingWorkspace, type ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import layout from "@/components/staff/IntakeWorkspace.module.css";
import styles from "@/components/staff/IntakeWorklist.module.css";
import workspace from "@/components/staff/StaffWorkspace.module.css";

const rowId = (row: ListingItem) => row.vehicleId;
const columns: DataColumn<ListingItem>[] = [
  {
    id: "registration",
    label: "Registration",
    sortable: true,
    minWidth: 145,
    cell: (row) => row.registrationPlate,
  },
  {
    id: "vehicle",
    label: "Vehicle",
    sortable: true,
    minWidth: 230,
    cell: (row) => `${row.manufactureYear} ${row.make} ${row.model} ${row.trim}`,
  },
  { id: "branch", label: "Branch", sortable: true, minWidth: 160, cell: (row) => row.branchName },
  {
    id: "inspection",
    label: "Inspection status",
    sortable: true,
    minWidth: 175,
    cell: (row) =>
      row.vehicleStatus === "PendingInspection" ? "Pending inspection" : row.vehicleStatus,
  },
  {
    id: "price",
    label: "Draft price",
    sortable: true,
    minWidth: 160,
    align: "right",
    cell: (row) => (row.vehiclePrice === null ? "Not set" : formatCurrency(row.vehiclePrice)),
  },
  {
    id: "publication",
    label: "Publication",
    sortable: true,
    minWidth: 230,
    cell: listingPublication,
  },
];

export function ListingTable({
  actor,
  initial,
  filters,
  page,
  branches,
  listingId,
  selected,
  section,
}: {
  actor: CurrentActor;
  initial: ApiResult<Page<ListingItem>>;
  filters: Filters;
  page: number;
  branches: Page<Branch> | null;
  listingId: string;
  selected: ApiResult<ListingWorkspace> | null;
  section: "listing" | "publication";
}) {
  const state = useListingTable(initial, filters, page, branches);
  const drawer = useStaffSelection(listingId, selected, {
    parameter: "listing",
    path: (id) => `/api/staff/listings/${id}`,
    parse: parseListingWorkspace,
    matches: (value, id) => value.vehicle.vehicleId === id,
    read: (path, parse, options) => apiFetch(path, parse, options, { requireActor: true }),
    commitAfterRead: true,
  });
  const listingColumns = columns.map((column) =>
    column.id === "registration"
      ? {
          ...column,
          cell: (row: ListingItem) => (
            <Button
              size="compact"
              variant="ghost"
              onClick={() => void drawer.choose(row.vehicleId)}
            >
              {row.registrationPlate}
            </Button>
          ),
        }
      : column,
  );
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
  const filtered = Boolean(active.filters.search || active.filters.branchId);
  const filterControls = <ListingFilters state={state} group={actor.role === "GeneralManager"} />;

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
    <div className={layout.workspace}>
      <LoadingProgress label="Loading listings" active={state.busy} />
      <StaffCommandBar
        title="Listings"
        actions={
          <RefreshButton
            label="Refresh listings"
            busy={state.busy}
            disabled={!state.ready}
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
            <DataTableWorkspace label="Listings">
              <RecordToolbar
                filters={filterControls}
                filterCount={applied.length}
                disabled={false}
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
                <form
                  className={styles.search}
                  onSubmit={(event) => {
                    event.preventDefault();
                    void state.load({ ...active.filters, search: state.draft.search.trim() }, 1);
                  }}
                >
                  <SearchField
                    label="Search listings"
                    placeholder="Search vehicle or registration"
                    value={state.draft.search}
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
              <DataTable
                label="Listings"
                rows={snapshot.items}
                columns={listingColumns}
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
                selectedRowId={drawer.id}
                onRowClick={(row) => void drawer.choose(row.vehicleId)}
                emptyState={{
                  title: filtered ? "No listings match your criteria" : "No vehicles to list yet",
                  description: filtered
                    ? "Try another search or clear the filters."
                    : "Checked-in vehicles will appear here for listing preparation.",
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
              <p>Listings are unavailable. Retry when the service is available.</p>
              <Button size="compact" variant="secondary" onClick={() => void state.load()}>
                Retry listings
              </Button>
              {filterControls}
            </div>
          )}
        </div>
      </div>
      <ListingDrawer selection={drawer} initial={selected} initialSection={section} />
    </div>
  );
}
