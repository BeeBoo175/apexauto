import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import type { AppliedWorklistFilter } from "@/lib/staff/filter-summary";
import type { StaffFilters } from "@/lib/staff/contracts";
import styles from "./WorklistFilters.module.css";

export function AppliedWorklistFilters({
  filters,
  disabled,
  onRemove,
}: {
  filters: AppliedWorklistFilter[];
  disabled: boolean;
  onRemove: (filters: StaffFilters) => void;
}) {
  if (filters.length === 0) {
    return null;
  }

  return (
    <div className={styles.summary} role="group" aria-label="Applied appointment filters">
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
