import { Button } from "@/components/ui/Button";
import type { PointerEvent } from "react";
import { Icon } from "@/components/ui/Icon";
import type { DataColumn, TableSorting } from "./types";
import styles from "./DataTable.module.css";

export function DataTableHeader<Row>({
  column,
  sorting,
  disabled,
  resizable,
  onStart,
}: {
  column: DataColumn<Row>;
  sorting?: TableSorting;
  disabled: boolean;
  resizable: boolean;
  onStart: (event: PointerEvent<HTMLDivElement>) => void;
}) {
  const sort = sorting?.value;
  const sortable = Boolean(column.sortable && sorting);
  const active = sort?.column === column.id;
  const sortClassName = active ? styles.sortActive : styles.sortInactive;
  let direction: "ascending" | "descending" | "none" = "none";
  if (active) {
    direction = sort?.direction === "asc" ? "ascending" : "descending";
  }
  const sortIcons = {
    none: "sort-neutral",
    ascending: "sort-ascending",
    descending: "sort-descending",
  } as const;

  function cycleSort() {
    if (!sorting) {
      return;
    }

    if (active && sort?.direction === "desc") {
      sorting.onChange(null);
      return;
    }
    sorting.onChange({
      column: column.id,
      direction: active ? "desc" : "asc",
    });
  }
  return (
    <th
      scope="col"
      data-column-id={column.id}
      className={styles.header}
      aria-sort={sortable ? direction : undefined}
    >
      <div className={styles.headerContent}>
        {sortable ? (
          <Button
            variant="ghost"
            size="row"
            disabled={disabled}
            onClick={cycleSort}
            aria-label={`Sort by ${column.label}`}
          >
            <span className={styles.headerLabel}>{column.label}</span>
            <span aria-hidden="true" className={sortClassName}>
              <Icon name={sortIcons[direction]} />
            </span>
          </Button>
        ) : (
          <span className={styles.headerLabel}>{column.label}</span>
        )}
      </div>
      {resizable && (
        <div
          aria-hidden="true"
          data-resize-column={column.id}
          className={styles.resize}
          onPointerDown={(event) => {
            if (disabled) {
              return;
            }
            onStart(event);
          }}
        />
      )}
    </th>
  );
}
