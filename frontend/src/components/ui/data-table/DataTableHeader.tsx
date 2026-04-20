import { Button } from "@/components/ui/Button";
import type { PointerEvent } from "react";
import { Icon } from "@/components/ui/Icon";
import type { DataColumn, TableSorting } from "./types";
import styles from "./DataTableHeader.module.css";
import { DataTableColumnFilter } from "./DataTableColumnFilter";
import iconStyles from "./TableIcons.module.css";

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
  let direction: "ascending" | "descending" | "none" = "none";
  if (active) {
    direction = sort?.direction === "asc" ? "ascending" : "descending";
  }
  const sortIcons = {
    ascending: "table-sort-ascending",
    descending: "table-sort-descending",
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
      data-align={column.align ?? "left"}
      className={styles.header}
      aria-sort={sortable ? direction : undefined}
    >
      <div className={styles.headerContent}>
        {sortable ? (
          <Button
            variant="ghost"
            size="row"
            className={`${styles.sortButton} ${styles.action}`}
            disabled={disabled}
            onClick={cycleSort}
            aria-label={`Sort by ${column.label}`}
          >
            <span className={styles.headerLabel}>{column.label}</span>
            {active && (
              <span aria-hidden="true" className={`${styles.sortIndicator} ${iconStyles.glyph}`}>
                <Icon
                  name={sortIcons[sort?.direction === "desc" ? "descending" : "ascending"]}
                  grid={16}
                />
              </span>
            )}
          </Button>
        ) : (
          <span className={styles.headerLabel}>{column.label}</span>
        )}
        {column.filter && (
          <DataTableColumnFilter label={column.label} filter={column.filter} disabled={disabled} />
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
