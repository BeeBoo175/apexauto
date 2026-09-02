import { useState } from "react";
import { Dialog } from "@/components/ui/Dialog";
import { Icon } from "@/components/ui/Icon";
import type { DataColumn, TableSort } from "./types";
import styles from "./TableSortSheet.module.css";

const directions = ["asc", "desc"] as const;

type SortDirection = TableSort["direction"];

function directionText(direction: SortDirection, labels?: { asc: string; desc: string }): string {
  return labels?.[direction] ?? (direction === "asc" ? "Ascending" : "Descending");
}

export function TableSortSheet<Row>({
  columns,
  value,
  busy,
  onChange,
  onClose,
  label,
  directionLabels = {},
}: {
  columns: readonly DataColumn<Row>[];
  value: TableSort | null;
  busy: boolean;
  onChange: (sort: TableSort | null) => Promise<boolean>;
  onClose: () => void;
  label: string;
  directionLabels?: Record<string, { asc: string; desc: string }>;
}) {
  const [pending, setPending] = useState(false);
  const [closing, setClosing] = useState(false);
  const disabled = busy || pending;
  const sortable = columns.filter((column) => column.sortable);
  const current = sortable.find((column) => column.id === value?.column);

  async function apply(sort: TableSort | null) {
    setPending(true);
    try {
      if (await onChange(sort)) {
        setClosing(true);
      }
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog
      open
      title={`Sort ${label}`}
      description={
        current && value
          ? `${current.label}: ${directionText(value.direction, directionLabels[current.id])}`
          : "Default order"
      }
      variant="sheet"
      closing={closing}
      onClose={() => setClosing(true)}
      onClosed={onClose}
      closeDisabled={pending}
    >
      <div className={styles.options}>
        <button
          type="button"
          className={styles.defaultOption}
          aria-pressed={!value}
          disabled={disabled}
          onClick={() => void apply(null)}
        >
          <span>Default order</span>
          {!value && <Icon name="check" />}
        </button>
        {sortable.map((column) => (
          <div
            key={column.id}
            role="group"
            aria-label={column.label}
            className={styles.column}
            data-active={value?.column === column.id || undefined}
          >
            <span className={styles.columnLabel}>{column.label}</span>
            <div className={styles.directions}>
              {directions.map((direction) => {
                const selected = value?.column === column.id && value.direction === direction;
                return (
                  <button
                    key={direction}
                    type="button"
                    className={styles.direction}
                    disabled={disabled}
                    aria-pressed={selected}
                    aria-label={`${column.label}, ${direction === "asc" ? "ascending" : "descending"}`}
                    onClick={() => void apply({ column: column.id, direction })}
                  >
                    <Icon
                      name={direction === "asc" ? "table-sort-ascending" : "table-sort-descending"}
                      grid={16}
                    />
                    <span>{directionText(direction, directionLabels[column.id])}</span>
                    {selected && <Icon name="check" />}
                  </button>
                );
              })}
            </div>
          </div>
        ))}
      </div>
    </Dialog>
  );
}
