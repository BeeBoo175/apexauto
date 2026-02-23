"use client";
import { useEffect, useRef, type ReactNode } from "react";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { DataColumn } from "@/components/ui/data-table/types";
import { SearchField } from "@/components/ui/SearchField";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { Button } from "@/components/ui/Button";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { appliedWorklistFilters } from "@/lib/staff/filter-summary";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { Page } from "@/lib/intake/contracts";
import { exportQueue } from "@/lib/inspection/export";
import { queueQuery } from "@/lib/inspection/query";
import { label } from "@/lib/inspection/contracts";
import type { QueueKind } from "@/lib/inspection/contracts";
import type { useInspectionQueue } from "@/hooks/use-inspection-queue";
import { QueueFilters } from "./QueueFilters";
import layout from "@/components/staff/IntakeWorkspace.module.css";
import styles from "@/components/staff/IntakeWorklist.module.css";

export function QueueView<T>({
  actor,
  kind,
  state,
  columns,
  rowId,
  onOpen,
  selectedRowId,
  parse,
  children,
  disabled = false,
}: {
  actor: CurrentActor;
  kind: QueueKind;
  state: ReturnType<typeof useInspectionQueue<T>>;
  columns: DataColumn<T>[];
  rowId: (row: T) => string;
  onOpen: (row: T) => void;
  selectedRowId?: string;
  parse: (value: unknown) => Page<T> | null;
  children?: ReactNode;
  disabled?: boolean;
}) {
  const title = kind === "vehicles" ? "Inspections" : "Repair requests";
  const { snapshot, active } = state;
  const exportController = useRef<AbortController | null>(null);
  useEffect(() => () => exportController.current?.abort(), []);
  const branchName =
    state.branches?.items.find((branch) => branch.id === active.filters.branchId)?.name ??
    "Selected branch";
  const applied = appliedWorklistFilters(active.filters, branchName, label);
  const filtered = applied.length > 0 || Boolean(active.filters.search);
  const emptyTitle = kind === "vehicles" ? "No inspection records yet" : "No repair requests yet";
  const emptyDescription =
    kind === "vehicles"
      ? "Checked-in vehicles will appear here when they are ready for inspection."
      : "Repair requests will appear here after work is requested from a signed inspection.";

  async function exportAll() {
    const controller = new AbortController();
    exportController.current = controller;
    try {
      await exportQueue(kind, active.filters, columns, parse, rowId, controller.signal);
    } catch (error) {
      if (!controller.signal.aborted) {
        throw error;
      }
    } finally {
      exportController.current = null;
    }
  }
  const filters = (
    <QueueFilters
      kind={kind}
      draft={state.draft}
      onDraft={state.setDraft}
      apply={(f) => state.load(f, 1)}
      busy={state.busy || disabled}
      group={actor.role === "GeneralManager"}
      branches={state.branches}
      loadBranches={(p) => void state.loadBranches(p)}
    />
  );
  return (
    <div className={layout.workspace}>
      <StaffCommandBar
        title={title}
        actions={
          <RefreshButton
            label={`Refresh ${title.toLowerCase()}`}
            busy={state.busy}
            disabled={!state.ready || disabled}
            onClick={() => void state.load()}
          />
        }
      />
      <div
        className={styles.page}
        aria-busy={state.busy}
        inert={!state.ready || state.busy || disabled}
      >
        <div className={styles.body}>
          {snapshot && (
            <DataTable
              fillHeight
              label={title}
              rows={snapshot.items}
              emptyState={{
                title: filtered ? `No ${title.toLowerCase()} match your criteria` : emptyTitle,
                description: filtered
                  ? "Try another search or clear the filters to see more records."
                  : emptyDescription,
                ...(filtered && {
                  action: {
                    label: "Clear search and filters",
                    onClick: () =>
                      void state.load(
                        {
                          ...active.filters,
                          status: "All",
                          search: "",
                          branchId: "",
                          from: "",
                          to: "",
                        },
                        1,
                      ),
                  },
                }),
              }}
              columns={columns}
              rowId={rowId}
              selectionScope={`${actorIdentity(actor)}:${queueQuery(active.filters, active.page)}:${state.revision}`}
              selectedRowId={selectedRowId}
              onRowClick={onOpen}
              exportFileName={kind}
              onExportAll={exportAll}
              page={snapshot}
              sort={null}
              onSort={() => {}}
              onPage={(p, pageSize) => void state.load({ ...active.filters, pageSize }, p)}
              disabled={disabled}
              busy={state.busy}
              filters={filters}
              onFiltersOpenChange={(open) => {
                if (!open) {
                  state.setDraft((draft) => ({
                    ...draft,
                    from: active.filters.from,
                    to: active.filters.to,
                    status: active.filters.status,
                    branchId: active.filters.branchId,
                  }));
                }
              }}
              filterCount={applied.length}
              filterSummary={
                <AppliedWorklistFilters
                  filters={applied}
                  disabled={disabled || state.busy}
                  onRemove={(filters) => void state.load(filters, 1)}
                />
              }
              toolbar={
                <form
                  className={styles.search}
                  onSubmit={(e) => {
                    e.preventDefault();
                    void state.load({ ...active.filters, search: state.draft.search.trim() }, 1);
                  }}
                >
                  <SearchField
                    label={`Search ${title.toLowerCase()}`}
                    placeholder="Search vehicle or registration"
                    value={state.draft.search}
                    disabled={disabled}
                    onChange={(search) => state.setDraft({ ...state.draft, search })}
                    onClear={
                      active.filters.search
                        ? () => void state.load({ ...active.filters, search: "" }, 1)
                        : undefined
                    }
                  />
                </form>
              }
            />
          )}
          {!snapshot && (
            <div className={styles.unavailableFilters}>
              <p>
                The {title.toLowerCase()} queue is unavailable. Retry when the service is available.
              </p>
              <Button size="compact" variant="secondary" onClick={() => void state.load()}>
                Retry queue
              </Button>
              {filters}
            </div>
          )}
        </div>
      </div>
      {children}
    </div>
  );
}
