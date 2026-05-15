"use client";

import { useState, type ReactNode } from "react";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { Button } from "@/components/ui/Button";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { DataColumn, TableSort } from "@/components/ui/data-table/types";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { SearchField } from "@/components/ui/SearchField";
import type { ListSnapshot } from "@/hooks/use-settings-workspace";
import { emptyListFilters, type ListFilters } from "@/lib/settings/settings";
import type { AppliedFilter } from "@/lib/staff/filter-summary";
import type { LoadList } from "./settings-filters";
import styles from "@/components/staff/StaffWorklist.module.css";

// Administrator-only master data has no bulk action, so unlike operational tables it omits selection and CSV export.
export function MasterDataTable<Row extends { id: string }>({
  label,
  noun,
  tabs,
  snapshot,
  columns,
  applied,
  busy,
  selectedId,
  onLoad,
  onOpen,
  onCreate,
}: {
  label: string;
  noun: string;
  tabs: ReactNode;
  snapshot: ListSnapshot<Row>;
  columns: readonly DataColumn<Row>[];
  applied: AppliedFilter<ListFilters>[];
  busy: boolean;
  selectedId: string;
  onLoad: LoadList;
  onOpen: (row: Row) => void;
  onCreate: () => void;
}) {
  const { filters, page, data } = snapshot;
  const [search, setSearch] = useState(filters.search);
  const filtered = applied.length > 0 || Boolean(filters.search);

  function clearAll() {
    setSearch("");
    void onLoad(
      {
        ...emptyListFilters,
        pageSize: filters.pageSize,
        sort: filters.sort,
        direction: filters.direction,
      },
      1,
    );
  }

  function sort(next: TableSort | null) {
    void onLoad({ ...filters, sort: next?.column ?? "", direction: next?.direction ?? "asc" }, 1);
  }

  return (
    <DataTable
      toolbar={
        <>
          {tabs}
          <RecordToolbar
            filters={null}
            filterCount={applied.length}
            disabled={busy}
            filterSummary={
              <AppliedWorklistFilters
                filters={applied}
                disabled={busy}
                onRemove={(next) => void onLoad(next, 1)}
              />
            }
            actions={
              <>
                {filtered && (
                  <Button size="compact" variant="ghost" disabled={busy} onClick={clearAll}>
                    Reset filters
                  </Button>
                )}
                <Button size="compact" disabled={busy} onClick={onCreate}>
                  New {noun}
                </Button>
              </>
            }
          >
            <form
              className={styles.search}
              onSubmit={(event) => {
                event.preventDefault();
                if (!busy) {
                  void onLoad({ ...filters, search: search.trim() }, 1);
                }
              }}
            >
              <SearchField
                label={`Search ${label.toLowerCase()}`}
                placeholder={`Search ${label.toLowerCase()}`}
                value={search}
                disabled={false}
                busy={busy}
                onChange={setSearch}
                onClear={
                  filters.search
                    ? () => {
                        setSearch("");
                        void onLoad({ ...filters, search: "" }, 1);
                      }
                    : undefined
                }
              />
            </form>
          </RecordToolbar>
        </>
      }
      label={label}
      rows={data.items}
      columns={columns}
      sorting={{
        value: filters.sort ? { column: filters.sort, direction: filters.direction } : null,
        onChange: sort,
      }}
      rowId={(row) => row.id}
      page={{ page, pageSize: data.pageSize, total: data.total }}
      onPage={(nextPage, pageSize) => void onLoad({ ...filters, pageSize }, nextPage)}
      busy={busy}
      disabled={false}
      selectedRowId={selectedId}
      onRowClick={onOpen}
      emptyState={{
        title: filtered
          ? `No ${label.toLowerCase()} match your criteria`
          : `No ${label.toLowerCase()} yet`,
        description: `Search, use the column filters, or create a ${noun}.`,
        ...(filtered && { action: { label: "Clear search and filters", onClick: clearAll } }),
      }}
    />
  );
}
