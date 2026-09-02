import { Button } from "./Button";
import styles from "./RecordToolbar.module.css";

// The toolbar's reset for applied filters. A table with the mobile Sort and Filter buttons clears filters in the filter
// sheet and the applied chips instead, so the toolbar button steps aside there.
export function ResetFiltersButton({
  disabled,
  onClick,
}: {
  disabled: boolean;
  onClick: () => void;
}) {
  return (
    <Button
      size="compact"
      variant="ghost"
      className={styles.resetFilters}
      disabled={disabled}
      onClick={onClick}
    >
      Reset filters
    </Button>
  );
}
