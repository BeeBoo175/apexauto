import { Button } from "../Button";
import { Icon } from "../Icon";
import type { DataColumn, TableSort } from "./types";
import styles from "./DataTable.module.css";

export function DataTableHeader<Row>({
  column,
  sort,
  disabled,
  onSort,
}: {
  column: DataColumn<Row>;
  sort: TableSort | null;
  disabled: boolean;
  onSort: (sort: TableSort | null) => void;
}) {
  const active = sort?.column === column.id;
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
    if (active && sort?.direction === "desc") {
      onSort(null);
      return;
    }
    onSort({
      column: column.id,
      direction: active ? "desc" : "asc",
    });
  }
  return (
    <th
      scope="col"
      data-column-id={column.id}
      className={styles.header}
      aria-sort={column.sortable ? direction : undefined}
    >
      <div className={styles.headerContent}>
        {column.sortable ? (
          <Button
            variant="ghost"
            size="row"
            disabled={disabled}
            onClick={cycleSort}
            aria-label={`Sort by ${column.label}`}
          >
            {column.label}
            <span aria-hidden="true" className={active ? styles.sortActive : styles.sortInactive}>
              <Icon name={sortIcons[direction]} />
            </span>
          </Button>
        ) : (
          <span>{column.label}</span>
        )}
      </div>
    </th>
  );
}
