import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { ScrollStrip } from "@/components/ui/ScrollStrip";
import { buildAppliedCataloguePills } from "@/lib/vehicles/catalogue-controls";
import type { CatalogueFilters, CataloguePage } from "@/lib/vehicles/contracts";
import { catalogueDefaults } from "@/lib/vehicles/query";
import styles from "./CatalogueToolbar.module.css";

interface CatalogueToolbarProps {
  active: CatalogueFilters;
  page: number;
  snapshot: CataloguePage | null;
  busy: boolean;
  onApply: (filters: CatalogueFilters) => void;
}

export function CatalogueToolbar({ active, page, snapshot, busy, onApply }: CatalogueToolbarProps) {
  const appliedPills = buildAppliedCataloguePills(active);

  const firstItem = snapshot && snapshot.items.length > 0 ? (page - 1) * snapshot.pageSize + 1 : 0;
  const lastItem =
    snapshot && snapshot.items.length > 0 ? firstItem + snapshot.items.length - 1 : 0;

  function renderSummary() {
    if (!snapshot) {
      return <span>Vehicle inventory unavailable</span>;
    }
    const noun = snapshot.total === 1 ? "vehicle" : "vehicles";
    if (snapshot.items.length === 0) {
      return (
        <>
          <strong>{snapshot.total}</strong> inspected {noun}
        </>
      );
    }
    return (
      <>
        Showing{" "}
        <strong>
          {firstItem}–{lastItem}
        </strong>{" "}
        of <strong>{snapshot.total}</strong> inspected {noun}
      </>
    );
  }

  return (
    <div className={styles.controlsBar}>
      <div className={styles.resultsToolbar}>
        <p className={styles.summary} aria-live="polite">
          {renderSummary()}
        </p>
      </div>

      {appliedPills.length > 0 && (
        <ScrollStrip label="Applied vehicle filters" className={styles.appliedBar} dragScroll>
          {appliedPills.map((pill) => (
            <Button
              key={pill.key}
              type="button"
              variant="ghost"
              size="sm"
              className={styles.appliedPill}
              disabled={busy}
              aria-label={`Remove filter: ${pill.label}`}
              onClick={() => onApply(pill.without)}
            >
              <span className={styles.pillText}>{pill.label}</span>
              <Icon name="close" />
            </Button>
          ))}
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className={styles.clearAllButton}
            disabled={busy}
            onClick={() => onApply({ ...catalogueDefaults })}
          >
            Clear all
          </Button>
        </ScrollStrip>
      )}
    </div>
  );
}
