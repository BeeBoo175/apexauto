import { Button } from "./Button";
import styles from "./RecordToolbar.module.css";

export function CsvExportActions({
  selected,
  disabled,
  busy,
  onClear,
  onSelected,
  onAll,
}: {
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
        <span className={styles.meta} role="status">
          {selected} selected
        </span>
      )}
      {hasSelection && (
        <Button variant="ghost" size="compact" disabled={disabled} onClick={onClear}>
          Clear selection
        </Button>
      )}
      <Button
        variant="secondary"
        size="compact"
        disabled={disabled || busy}
        onClick={hasSelection ? onSelected : onAll}
      >
        {busy ? "Preparing CSV…" : exportLabel}
      </Button>
    </div>
  );
}
