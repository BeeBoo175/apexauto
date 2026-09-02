import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import type { DataColumn } from "./types";
import styles from "./ColumnFilterSheet.module.css";

// The mobile counterpart of the desktop column filter popovers: every filterable column keeps its own form, listed in
// one sheet, so a filter is defined once and works at both widths.
export function ColumnFilterSheet<Row>({
  label,
  columns,
  busy,
  onClear,
  onClose,
}: {
  label: string;
  columns: readonly DataColumn<Row>[];
  busy: boolean;
  onClear?: () => void;
  onClose: () => void;
}) {
  const [closing, setClosing] = useState(false);
  const filterable = columns.filter((column) => column.filter);

  return (
    <Dialog
      open
      title={`Filter ${label}`}
      variant="sheet"
      closing={closing}
      onClose={() => setClosing(true)}
      onClosed={onClose}
    >
      <div className={styles.sections}>
        {filterable.map((column) => (
          <section
            key={column.id}
            className={styles.section}
            data-active={column.filter?.active || undefined}
            aria-label={`${column.label} filter`}
          >
            <h3 className={styles.heading}>
              <span>{column.label}</span>
              {column.filter?.active && <span className={styles.applied}>Applied</span>}
            </h3>
            {column.filter?.content}
          </section>
        ))}
      </div>
      <div className={styles.footer}>
        {onClear && (
          <Button
            type="button"
            variant="ghost"
            size="compact"
            disabled={busy}
            onClick={() => {
              onClear();
              setClosing(true);
            }}
          >
            Clear all
          </Button>
        )}
        <Button type="button" size="compact" onClick={() => setClosing(true)}>
          Done
        </Button>
      </div>
    </Dialog>
  );
}
