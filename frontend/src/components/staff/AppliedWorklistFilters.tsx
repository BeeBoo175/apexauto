import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { ScrollStrip } from "@/components/ui/ScrollStrip";
import type { AppliedFilter } from "@/lib/staff/filter-summary";
import styles from "./WorklistFilters.module.css";

export function AppliedWorklistFilters<Filters>({
  filters,
  disabled,
  onRemove,
}: {
  filters: AppliedFilter<Filters>[];
  disabled: boolean;
  onRemove: (filters: Filters) => void;
}) {
  if (filters.length === 0) {
    return null;
  }

  return (
    <ScrollStrip label="Applied filters" className={styles.summary}>
      {filters.map((filter) => {
        const without = filter.without;
        if (filter.fixed) {
          return (
            <span key={filter.key} className={styles.chip} title={filter.label}>
              <span>{filter.label}</span>
            </span>
          );
        }
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
    </ScrollStrip>
  );
}
