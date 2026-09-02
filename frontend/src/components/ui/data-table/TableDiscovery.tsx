import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { ColumnFilterSheet } from "./ColumnFilterSheet";
import { TableSortSheet } from "./TableSortSheet";
import type { DataColumn, TableSort, TableSorting } from "./types";
import styles from "./TableDiscovery.module.css";

// The mobile Sort and Filter buttons of a table toolbar and the sheets they open. Both read the table's own columns,
// so a column's sorting and its desktop filter form carry over without a second definition.
export function TableDiscovery<Row>({
  label,
  columns,
  sorting,
  filterCount,
  disabled,
  directionLabels,
  onClearFilters,
}: {
  label: string;
  columns: readonly DataColumn<Row>[];
  sorting?: TableSorting;
  filterCount: number;
  disabled: boolean;
  directionLabels?: Record<string, { asc: string; desc: string }>;
  onClearFilters?: () => void;
}) {
  const [open, setOpen] = useState<"sort" | "filter" | null>(null);
  const sort = sorting?.value ?? null;
  const filterable = columns.some((column) => column.filter);
  const sortable = Boolean(sorting && columns.some((column) => column.sortable));
  const sortedColumn = columns.find((column) => column.id === sort?.column);
  let sortSummary = "";
  let sortIcon: "table-sort-ascending" | "table-sort-descending" = "table-sort-descending";
  if (sort) {
    sortIcon = sort.direction === "asc" ? "table-sort-ascending" : "table-sort-descending";
    sortSummary = `, ${sortedColumn?.label ?? sort.column} ${sort.direction === "asc" ? "ascending" : "descending"}`;
  }

  async function applySort(next: TableSort | null) {
    sorting?.onChange(next);
    return true;
  }

  return (
    <>
      {sortable && (
        <Button
          variant="secondary"
          size="compact"
          className={styles.button}
          data-sorted={sort ? true : undefined}
          aria-label={`Sort ${label}${sortSummary}`}
          aria-haspopup="dialog"
          disabled={disabled}
          onClick={() => setOpen("sort")}
        >
          <Icon name={sortIcon} grid={16} />
          <span className={styles.label}>Sort</span>
        </Button>
      )}
      {filterable && (
        <Button
          variant="secondary"
          size="compact"
          className={styles.button}
          data-filtered={filterCount > 0 ? true : undefined}
          aria-label={`Filter ${label}${filterCount ? `, ${filterCount} applied` : ""}`}
          aria-haspopup="dialog"
          disabled={disabled}
          onClick={() => setOpen("filter")}
        >
          <Icon name="filter" />
          <span className={styles.label}>Filter</span>
          {filterCount > 0 && <span className={styles.count}>{filterCount}</span>}
        </Button>
      )}
      {open === "sort" && sortable && (
        <TableSortSheet
          label={label}
          directionLabels={directionLabels}
          columns={columns}
          value={sort}
          busy={disabled}
          onChange={applySort}
          onClose={() => setOpen(null)}
        />
      )}
      {open === "filter" && filterable && (
        <ColumnFilterSheet
          label={label}
          columns={columns}
          busy={disabled}
          onClear={filterCount > 0 ? onClearFilters : undefined}
          onClose={() => setOpen(null)}
        />
      )}
    </>
  );
}
