import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import type { TableSort } from "./types";
import iconStyles from "./TableIcons.module.css";
import styles from "./DataTableMobile.module.css";

// A mobile column heading that sorts like the desktop header: the arrow shows only while its column is the sort.
export function MobileSortButton({
  label,
  sort,
  disabled,
  onClick,
}: {
  label: string;
  sort: TableSort["direction"] | null;
  disabled: boolean;
  onClick: () => void;
}) {
  return (
    <Button
      variant="ghost"
      size="compact"
      className={styles.sortButton}
      disabled={disabled}
      onClick={onClick}
      aria-label={`Sort by ${label}`}
    >
      <span>{label}</span>
      {sort && (
        <span aria-hidden="true" className={`${iconStyles.glyph} ${styles.sortIndicator}`}>
          <Icon
            name={sort === "desc" ? "table-sort-descending" : "table-sort-ascending"}
            grid={16}
          />
        </span>
      )}
    </Button>
  );
}
