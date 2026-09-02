import type { ReactNode } from "react";
import { Button } from "./Button";
import styles from "./RecordToolbar.module.css";

// The group reads left to right as selection, create, export, so a create action sits beside the export it pairs with.
export function CsvExportActions({
  create,
  selected,
  disabled,
  busy,
  onClear,
  onSelected,
  onAll,
}: {
  create?: ReactNode;
  selected: number;
  disabled: boolean;
  busy: boolean;
  onClear: () => void;
  onSelected: () => void;
  onAll: () => void;
}) {
  const hasSelection = selected > 0;
  const exportLabel = hasSelection ? "Export selected CSV" : "Export full CSV";

  return (
    <div className={styles.exportActions} role="group" aria-label="Table actions">
      {hasSelection && (
        <div className={styles.selectionBar}>
          <span className={styles.meta} role="status">
            {selected} selected
          </span>
          <Button variant="ghost" size="compact" disabled={disabled} onClick={onClear}>
            Clear selection
          </Button>
        </div>
      )}
      {create && <div className={styles.createSlot}>{create}</div>}
      <Button
        variant="secondary"
        size="compact"
        className={styles.exportButton}
        disabled={disabled || busy}
        onClick={hasSelection ? onSelected : onAll}
      >
        {busy ? "Preparing CSV…" : exportLabel}
      </Button>
    </div>
  );
}
