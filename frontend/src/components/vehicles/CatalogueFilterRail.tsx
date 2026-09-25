import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import type { CatalogueFilters as Filters } from "@/lib/vehicles/contracts";
import styles from "./CatalogueFilters.module.css";
import layout from "./VehicleLayout.module.css";

// Wide screens keep the filters in a rail beside the cars.
export function CatalogueFilterRail({
  draft,
  apply,
  fields,
  resetIdle,
  busy,
  onReset,
}: {
  draft: Filters;
  apply: (filters: Filters) => void;
  fields: ReactNode;
  resetIdle: boolean;
  busy: boolean;
  onReset: () => void;
}) {
  return (
    <aside className={styles.rail} aria-label="Filter cars">
      <div className={styles.railHeader}>
        <h2 className={layout.cardHeading}>Filter vehicles</h2>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className={styles.resetButton}
          data-idle={resetIdle || undefined}
          tabIndex={resetIdle ? -1 : undefined}
          disabled={busy}
          onClick={onReset}
        >
          Reset
        </Button>
      </div>
      <form
        className={styles.railForm}
        method="get"
        action="/vehicles"
        onSubmit={(event) => {
          event.preventDefault();
          apply(draft);
        }}
      >
        {fields}
      </form>
    </aside>
  );
}
