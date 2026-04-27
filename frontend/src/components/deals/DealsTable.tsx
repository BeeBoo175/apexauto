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
import { useDealsTable, type DealPageData } from "@/hooks/use-deals-table";
import { useRowSelection } from "@/hooks/use-row-selection";
import type { ApiResult } from "@/lib/api";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { exportDeals, dealExportColumns } from "@/lib/deals/deal-export";
import {
  dealQuery,
  dealViews,
  defaultDealFilters,
  type DealFilters,
  type DealSummary,
  type DealView,
} from "@/lib/deals/deals";
import { dealColumns } from "./deal-columns";
import { dealTableFilters } from "./deal-filters";
import layout from "@/components/staff/IntakeWorkspace.module.css";
import styles from "@/components/staff/IntakeWorklist.module.css";
import workspace from "@/components/staff/StaffWorkspace.module.css";

const rowId = (row: DealSummary) => row.id;

export function DealsTable({
  actor,
  initial,
  filters,
  page,
}: {
  actor: CurrentActor;
  initial: ApiResult<DealPageData>;
  filters: DealFilters;
  page: number;
}) {
  const state = useDealsTable(initial, filters, page);
  const animateEntry = useContext(StaffPageEntranceContext);
  const { snapshot, active } = state;
  const { columns, applied } = dealTableFilters(
    active.filters,
    state.busy,
    dealColumns(actor.role),
    state.load,
  );
  const selection = useRowSelection(
    snapshot?.items ?? [],
    rowId,
    `${actorIdentity(actor)}:${dealQuery(active.filters, active.page)}:${state.revision}`,
  );
  const csv = useCsvExport(dealExportColumns, "deals", (signal) =>
    exportDeals(active.filters, signal),
  );
  const [search, setSearch] = useState(active.filters.search);
  const filtered = applied.length > 0 || Boolean(active.filters.search);
  const counts = snapshot?.counts;
  const tabs = dealViews.map((view) => {
    const total =
      counts?.[
        view.value === "NeedsApproval"
          ? "needsApproval"
          : (view.value.toLowerCase() as "active" | "history")
      ];
    return {
      value: view.value,
      label: total === undefined ? view.label : `${view.label} (${total})`,
    };
  });

  function changeView(view: DealView) {
    // Switching view restarts from that view's own ordering, as when the page first opens.
    const defaults = defaultDealFilters(actor.role);
    void state.load(
      {
        ...active.filters,
        view,
        sort: view === "NeedsApproval" ? "submitted" : defaults.sort,
        direction: view === "NeedsApproval" ? "asc" : "desc",
      },
      1,
    );
  }

  function reset() {
    setSearch("");
    const defaults = defaultDealFilters(actor.role);
    void state.load(
      { ...defaults, view: active.filters.view, pageSize: active.filters.pageSize },
      1,
    );
  }

  function sort(next: TableSort | null) {
    void state.load(
      { ...active.filters, sort: next?.column ?? "", direction: next?.direction ?? "asc" },
      1,
    );
  }

  return (
    <div className={layout.workspace}>
      <LoadingProgress label="Loading deals" active={state.busy} />
      <StaffCommandBar
        title="Deals"
        actions={
          <RefreshButton
            label="Refresh deals"
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
            <DataTable
              toolbar={
                <>
                  <StaffQueueTabs
                    label="Deal views"
                    tabs={tabs}
                    value={active.filters.view}
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
                        onRemove={(next) => void state.load(next, 1)}
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
                          void state.load({ ...active.filters, search: search.trim() }, 1);
                        }
                      }}
                    >
                      <SearchField
                        label="Search deals"
                        placeholder="Search deal, vehicle or customer"
                        value={search}
                        disabled={false}
                        busy={state.busy}
                        onChange={setSearch}
                        onClear={
                          active.filters.search
                            ? () => {
                                setSearch("");
                                void state.load({ ...active.filters, search: "" }, 1);
                              }
                            : undefined
                        }
                      />
                    </form>
                  </RecordToolbar>
                </>
              }
              label="Deals"
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
              onPage={(nextPage, pageSize) =>
                void state.load({ ...active.filters, pageSize }, nextPage)
              }
              busy={state.busy}
              disabled={false}
              emptyState={{
                title: filtered ? "No deals match your criteria" : "No deals in this view",
                description: "Use the column filters, or switch view.",
                ...(filtered && { action: { label: "Clear filters", onClick: reset } }),
              }}
            />
          ) : (
            <div className={styles.unavailableFilters}>
              <p>Deals are unavailable. Retry when the service is available.</p>
              <Button size="compact" variant="secondary" onClick={() => void state.load()}>
                Retry deals
              </Button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
