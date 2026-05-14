"use client";

import { useContext, useState } from "react";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { Button } from "@/components/ui/Button";
import { CsvExportActions } from "@/components/ui/CsvExportActions";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { TableSort } from "@/components/ui/data-table/types";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { SearchField } from "@/components/ui/SearchField";
import { useCsvExport } from "@/hooks/use-csv-export";
import { useMinuteClock } from "@/hooks/use-minute-clock";
import { useRowSelection } from "@/hooks/use-row-selection";
import { useSaleSelection } from "@/hooks/use-sale-selection";
import { useSalesTable } from "@/hooks/use-sales-table";
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
import type { SaleSection } from "@/lib/sales/sale-sections";
import { SaleDrawer } from "./SaleDrawer";
import { saleColumns } from "./sale-columns";
import { saleTableFilters } from "./sale-filters";
import styles from "@/components/staff/StaffWorklist.module.css";
import workspace from "@/components/staff/StaffWorkspace.module.css";

const rowId = (row: SaleSummary) => row.id;

const emptyTitles: Record<TableView, string> = {
  NeedsApproval: "No sales need approval",
  Open: "No open holds",
  BeingHandled: "No sales are being handled",
  Approved: "No approved sales",
  History: "No closed sales",
};

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
  const state = useSalesTable(initial, filters, page);
  const drawer = useSaleSelection(saleId, selected, section, version);
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
    void load({ ...active.filters, sort: next?.column ?? "", direction: next?.direction ?? "asc" });
  }

  return (
    <div className={styles.workspace}>
      <LoadingProgress label="Loading sales" active={state.busy} />
      <StaffCommandBar
        title="Sales"
        actions={
          <RefreshButton
            label="Refresh sales"
            busy={state.busy}
            disabled={!state.ready}
            onClick={() => void load(active.filters, active.page)}
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
                    label="Sale views"
                    countNoun="sales"
                    tabs={tabs}
                    value={view}
                    disabled={state.busy}
                    onChange={changeView}
                  />
                  <RecordToolbar
                    filters={null}
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
                        {filtered && (
                          <Button
                            size="compact"
                            variant="ghost"
                            disabled={state.busy}
                            onClick={reset}
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
              }
              label="Sales"
              rows={snapshot.items}
              columns={columns}
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
              onRowClick={(row) => void drawer.choose(row.id)}
              emptyState={{
                title: filtered ? "No sales match your criteria" : emptyTitles[view],
                description: "Use the column filters, or switch tab.",
                ...(filtered && { action: { label: "Clear filters", onClick: reset } }),
              }}
            />
          ) : (
            <div className={styles.unavailableFilters}>
              <p>Sales are unavailable. Retry when the service is available.</p>
              <Button
                size="compact"
                variant="secondary"
                onClick={() => void load(active.filters, active.page)}
              >
                Retry sales
              </Button>
            </div>
          )}
        </div>
      </div>
      <SaleDrawer
        key={`${actorIdentity(actor)}:${drawer.id}`}
        selection={drawer}
        now={now}
        onChanged={() => void load(active.filters, active.page)}
      />
    </div>
  );
}
