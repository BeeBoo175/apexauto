"use client";

import { useContext, useState, useTransition } from "react";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { useRouter } from "next/navigation";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { Button } from "@/components/ui/Button";
import { CsvExportActions } from "@/components/ui/CsvExportActions";
import { ResetFiltersButton } from "@/components/ui/ResetFiltersButton";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { TableSort } from "@/components/ui/data-table/types";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { SearchField } from "@/components/ui/SearchField";
import { Icon } from "@/components/ui/Icon";
import { TableSortSheet } from "@/components/ui/data-table/TableSortSheet";
import { useCsvExport } from "@/hooks/use-csv-export";
import { useMinuteClock } from "@/hooks/use-minute-clock";
import { useRowSelection } from "@/hooks/use-row-selection";
import { useSalesManager } from "@/hooks/use-sales-manager";
import type { ApiResult } from "@/lib/api";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import {
  type Sale,
  tableViews,
  type SaleSummary,
  type SalesTableFilters,
  type SalesTablePage,
  type TableView,
} from "@/lib/sales/contracts";
import { defaultTableFilters, tableQuery, tableViewFilters } from "@/lib/sales/query";
import { exportSales, saleExportColumns } from "@/lib/sales/sale-export";
import { listingSelectionHref } from "@/lib/vehicles/listing-location";
import type { SaleSection } from "@/lib/sales/sale-sections";
import { SaleDrawer } from "./SaleDrawer";
import { saleColumns } from "./sale-columns";
import { saleTableFilters } from "./sale-filters";
import { SalesFilterSheet } from "./SalesFilterSheet";
import { SaleMobileIdentity, SaleMobileMetric } from "./SaleMobileCells";
import { saleMetrics, saleSortLabels, type SaleMetric } from "./sale-mobile";
import tableMobile from "@/components/ui/data-table/DataTableMobile.module.css";
import discoveryStyles from "@/components/ui/data-table/TableDiscovery.module.css";
import styles from "@/components/staff/StaffWorklist.module.css";
import workspace from "@/components/staff/StaffWorkspace.module.css";
import { AssistedVehicleAction } from "@/components/staff/AssistedVehicleAction";

const rowId = (row: SaleSummary) => row.id;

// The managers' one Sales table: each tab is a step in the sale, and a row opens the same editor agents use.
export function SalesTable({
  actor,
  initial,
  filters,
  page,
  saleId,
  section,
  version,
  selected,
}: {
  actor: CurrentActor;
  initial: ApiResult<SalesTablePage>;
  filters: SalesTableFilters;
  page: number;
  saleId: string;
  section: SaleSection;
  version: number | null;
  selected: ApiResult<Sale> | null;
}) {
  const {
    table: state,
    drawer,
    refresh,
  } = useSalesManager({
    initial,
    filters,
    page,
    saleId,
    section,
    version,
    selected,
  });
  const router = useRouter();
  const [opening, startOpening] = useTransition();
  const animateEntry = useContext(StaffPageEntranceContext);
  // Hold timers are read against this moment, which moves forward with each confirmed read.
  const [now, setNow] = useMinuteClock();
  const { snapshot, active } = state;
  const view = (active.filters.view || snapshot?.view || "NeedsApproval") as TableView;
  const { columns, applied } = saleTableFilters(
    { ...active.filters, view },
    state.busy,
    saleColumns(actor.role, now),
    state.load,
  );
  const selection = useRowSelection(
    snapshot?.items ?? [],
    rowId,
    `${actorIdentity(actor)}:${tableQuery(active.filters, active.page)}:${state.revision}`,
  );
  const showBranch = actor.role === "GeneralManager";
  const csv = useCsvExport(saleExportColumns(showBranch, now), "sales", (signal) =>
    exportSales(active.filters, signal, showBranch),
  );
  const [search, setSearch] = useState(active.filters.search);
  const [metric, setMetric] = useState<SaleMetric>("balance");
  const [discovery, setDiscovery] = useState<"sort" | "filter" | null>(null);
  const metricHeading = saleMetrics.find((item) => item.value === metric)!.heading;
  const sorted = Boolean(active.filters.sort);
  const sortedColumn = columns.find((column) => column.id === active.filters.sort);
  const sortSummary = `${sortedColumn?.label ?? active.filters.sort}, ${
    active.filters.direction === "asc" ? "ascending" : "descending"
  }`;
  const filtered = applied.length > 0 || Boolean(active.filters.search);
  const tabs = tableViews.map((item) => ({
    value: item.value,
    label: item.label,
    count: snapshot?.counts[item.count],
  }));

  function load(next: SalesTableFilters, nextPage = 1) {
    setNow(Date.now());
    return state.load(next, nextPage);
  }

  function changeView(next: TableView) {
    void load(tableViewFilters({ ...active.filters, view }, next));
  }

  function reset() {
    setSearch("");
    void load({ ...defaultTableFilters(), view, pageSize: active.filters.pageSize });
  }

  function sort(next: TableSort | null) {
    return load({
      ...active.filters,
      sort: next?.column ?? "",
      direction: next?.direction ?? "asc",
    });
  }

  async function sortMobile(next: TableSort | null) {
    const confirmed = await sort(next);
    const lens = saleMetrics.find((item) => item.value === next?.column);
    if (confirmed && lens) {
      setMetric(lens.value);
    }
    return confirmed;
  }

  const toolbar = (
    <>
      <StaffQueueTabs
        label="Sale views"
        countNoun="sales"
        tabs={tabs}
        value={view}
        disabled={state.busy}
        onChange={changeView}
      />
      <RecordToolbar
        filters={null}
        mobileDiscovery={
          <>
            <Button
              variant="secondary"
              size="compact"
              className={discoveryStyles.button}
              data-sorted={sorted || undefined}
              aria-label={`Sort sales${sorted ? `, ${sortSummary}` : ""}`}
              aria-haspopup="dialog"
              disabled={state.busy}
              onClick={() => setDiscovery("sort")}
            >
              <Icon
                name={
                  sorted && active.filters.direction === "asc"
                    ? "table-sort-ascending"
                    : "table-sort-descending"
                }
                grid={16}
              />
              <span className={discoveryStyles.label}>Sort</span>
            </Button>
            <Button
              variant="secondary"
              size="compact"
              className={discoveryStyles.button}
              data-filtered={applied.length > 0 || undefined}
              aria-label={`Filter sales${applied.length ? `, ${applied.length} applied` : ""}`}
              aria-haspopup="dialog"
              disabled={state.busy}
              onClick={() => setDiscovery("filter")}
            >
              <Icon name="filter" />
              <span className={discoveryStyles.label}>Filter</span>
              {applied.length > 0 && (
                <span className={discoveryStyles.count}>{applied.length}</span>
              )}
            </Button>
          </>
        }
        filterCount={applied.length}
        disabled={state.busy}
        filterSummary={
          <AppliedWorklistFilters
            filters={applied}
            disabled={state.busy}
            onRemove={(next) => void load(next)}
          />
        }
        actions={
          <>
            {filtered && <ResetFiltersButton disabled={state.busy} onClick={reset} />}
            <CsvExportActions
              create={
                <AssistedVehicleAction
                  kind="reservation"
                  disabled={state.busy}
                  onClosed={() => void refresh()}
                />
              }
              selected={selection.selectedRows.length}
              disabled={
                state.busy || !snapshot || snapshot.total === 0 || view === "AwaitingDeposit"
              }
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
              void load({ ...active.filters, search: search.trim() });
            }
          }}
        >
          <SearchField
            label="Search sales"
            placeholder="Search reference, plate, VIN, vehicle, customer, contact, promo or agent"
            value={search}
            disabled={false}
            busy={state.busy}
            onChange={setSearch}
            onClear={
              active.filters.search
                ? () => {
                    setSearch("");
                    void load({ ...active.filters, search: "" });
                  }
                : undefined
            }
          />
        </form>
      </RecordToolbar>
    </>
  );

  const salesTable = snapshot ? (
    <DataTable
      toolbar={toolbar}
      label="Sales"
      rows={snapshot.items}
      columns={columns}
      mobile={{
        controls: (
          <div className={tableMobile.lenses} role="tablist" aria-label="Compare sales by">
            {saleMetrics.map((item) => (
              <Button
                key={item.value}
                variant="ghost"
                size="compact"
                role="tab"
                className={tableMobile.lens}
                aria-selected={metric === item.value}
                onClick={() => setMetric(item.value)}
              >
                {item.label}
              </Button>
            ))}
          </div>
        ),
        identityLabel: "Vehicle & order",
        metricLabel: metricHeading,
        metricColumn: metric,
        identity: (row) => <SaleMobileIdentity row={row} />,
        metric: (row) => (
          <SaleMobileMetric row={row} metric={metric} now={now} showBranch={showBranch} />
        ),
      }}
      sorting={{
        value: active.filters.sort
          ? { column: active.filters.sort, direction: active.filters.direction }
          : null,
        onChange: sort,
      }}
      rowId={rowId}
      selection={selection.selection}
      page={snapshot}
      onPage={(nextPage, pageSize) => void load({ ...active.filters, pageSize }, nextPage)}
      busy={state.busy}
      disabled={false}
      selectedRowId={drawer.id}
      onRowClick={(row) => {
        // A deposit request is not a sale; its row opens the listing that holds it and can cancel it.
        if (row.status === "AwaitingDeposit" && row.vehicleId) {
          const href = listingSelectionHref({
            id: row.vehicleId,
            section: "publication",
            record: null,
          });
          startOpening(() => router.push(href));
          return;
        }
        void drawer.choose(row.id);
      }}
      emptyState={{
        noun: "sales",
        icon: "file-text",
        narrowed: filtered,
        hint: "Sales appear here once a customer reserves a car.",
        onClear: reset,
      }}
    />
  ) : null;

  const unavailable = snapshot ? null : (
    <div className={styles.unavailableFilters}>
      <ReloadNotice subject="Sales" />
    </div>
  );

  return (
    <div className={styles.workspace}>
      <LoadingProgress label="Loading sales" active={state.busy || opening} />
      <StaffCommandBar
        title="Sales"
        actions={
          <RefreshButton
            label="Refresh sales"
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
          {salesTable}
          {unavailable}
        </div>
      </div>
      {discovery === "sort" && (
        <TableSortSheet
          label="sales"
          directionLabels={saleSortLabels}
          columns={columns}
          value={
            active.filters.sort
              ? { column: active.filters.sort, direction: active.filters.direction }
              : null
          }
          busy={state.busy}
          onChange={sortMobile}
          onClose={() => setDiscovery(null)}
        />
      )}
      {discovery === "filter" && (
        <SalesFilterSheet
          filters={{ ...active.filters, view }}
          showBranch={showBranch}
          busy={state.busy}
          load={load}
          onClose={() => setDiscovery(null)}
        />
      )}
      <SaleDrawer
        key={`${actorIdentity(actor)}:${drawer.id}`}
        selection={drawer}
        now={now}
        onChanged={() => void refresh()}
      />
    </div>
  );
}
