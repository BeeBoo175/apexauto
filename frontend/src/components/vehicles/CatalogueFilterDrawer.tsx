import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import type { CatalogueFilters as Filters } from "@/lib/vehicles/contracts";
import styles from "./CatalogueFilters.module.css";

// Phones open the filters in a drawer.
export function CatalogueFilterDrawer({
  open,
  onClose,
  draft,
  apply,
  fields,
  activeCount,
  resetDisabled,
  onReset,
}: {
  open: boolean;
  onClose: () => void;
  draft: Filters;
  apply: (filters: Filters) => void;
  fields: ReactNode;
  activeCount: number;
  resetDisabled: boolean;
  onReset: () => void;
}) {
  return (
    <Dialog
      open={open}
      onClose={onClose}
      variant="drawer"
      density="compact"
      title="Filter cars"
      description={
        activeCount > 0
          ? `${activeCount} ${activeCount === 1 ? "filter" : "filters"} applied`
          : "Narrow the cars by what matters to you"
      }
      headerActions={
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className={styles.resetButton}
          disabled={resetDisabled}
          onClick={onReset}
        >
          Reset
        </Button>
      }
    >
      <form
        className={styles.filters}
        method="get"
        action="/vehicles"
        onSubmit={(event) => {
          event.preventDefault();
          apply(draft);
        }}
      >
        {fields}
        <div className={styles.railActions}>
          <Button type="button" size="md" className={styles.doneButton} onClick={onClose}>
            Show results
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
