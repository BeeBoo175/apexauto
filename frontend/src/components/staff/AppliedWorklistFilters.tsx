import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import type { AppliedWorklistFilter } from "@/lib/staff/filter-summary";
import styles from "./WorklistFilters.module.css";

export function AppliedWorklistFilters<Filters>({
  filters,
  disabled,
  onRemove,
}: {
  filters: AppliedWorklistFilter<Filters>[];
  disabled: boolean;
  onRemove: (filters: Filters) => void;
}) {
  if (filters.length === 0) {
    return null;
  }

  return (
    <div className={styles.summary} role="group" aria-label="Applied filters">
      {filters.map((filter) => {
        const without = filter.without;
        return (
          <Button
            key={filter.key}
            size="row"
            variant="ghost"
            className={styles.chip}
            disabled={disabled}
            title={filter.label}
            aria-label={`Remove ${filter.key} filter: ${filter.label}`}
            onClick={() => onRemove(without)}
          >
            <span>{filter.label}</span>
            <span className={styles.removeIcon}>
              <Icon name="close" />
            </span>
          </Button>
        );
      })}
    </div>
  );
}
