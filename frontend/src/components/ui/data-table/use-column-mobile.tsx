import { useState } from "react";
import { Button } from "@/components/ui/Button";
import type { DataColumn, MobileTable } from "./types";
import styles from "./DataTableMobile.module.css";

// Derives the comparative mobile table from a table's columns: the first two identify a row and every other column is
// a tab for the value shown beside it, so a table needs no second description of its data for phones.
export function useColumnMobile<Row>(
  columns: readonly DataColumn<Row>[],
  enabled: boolean,
): MobileTable<Row> | undefined {
  const [chosen, setChosen] = useState("");
  const identity = columns.length > 2 ? columns.slice(0, 2) : columns.slice(0, 1);
  const metrics = columns.slice(identity.length);
  if (!enabled || identity.length === 0 || metrics.length === 0) {
    return undefined;
  }

  const metric = metrics.find((column) => column.id === chosen) ?? metrics[0];
  const [primary, secondary] = identity;
  return {
    controls: (
      <div className={styles.lenses} role="tablist" aria-label="Show column">
        {metrics.map((column) => (
          <Button
            key={column.id}
            variant="ghost"
            size="compact"
            role="tab"
            className={styles.lens}
            aria-selected={metric.id === column.id}
            onClick={() => setChosen(column.id)}
          >
            {column.label}
          </Button>
        ))}
      </div>
    ),
    identityColumns: identity.map((column) => ({ id: column.id, label: column.label })),
    metricLabel: metric.label,
    metricColumn: metric.id,
    identity: (row) => (
      <div className={styles.columnIdentity}>
        <span className={styles.columnPrimary}>{primary.cell(row)}</span>
        {secondary && <span className={styles.columnSecondary}>{secondary.cell(row)}</span>}
      </div>
    ),
    metric: (row) => <div className={styles.columnMetric}>{metric.cell(row)}</div>,
  };
}
