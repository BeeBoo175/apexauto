import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import type { CatalogueFilters } from "@/lib/vehicles/contracts";
import { CatalogueSearch } from "./CatalogueSearch";
import { CatalogueSort } from "./CatalogueSort";
import styles from "./CatalogueControlBar.module.css";
import layout from "./VehicleLayout.module.css";

interface CatalogueControlBarProps {
  active: CatalogueFilters;
  activeFilterCount: number;
  busy: boolean;
  filtersOpen: boolean;
  onSearch: (search: string) => void;
  onSort: (sort: CatalogueFilters["sort"], direction: CatalogueFilters["direction"]) => void;
  onOpenFilters: () => void;
}

// Search, sort and the filter drawer button stay under the site header while the cars scroll.
export function CatalogueControlBar({
  active,
  activeFilterCount,
  busy,
  filtersOpen,
  onSearch,
  onSort,
  onOpenFilters,
}: CatalogueControlBarProps) {
  const filterLabel =
    activeFilterCount > 0
      ? `Filters, ${activeFilterCount} ${activeFilterCount === 1 ? "filter" : "filters"} applied`
      : "Filters";

  return (
    <div className={styles.bar}>
      <div className={`${layout.content} ${styles.inner}`}>
        <div className={styles.group}>
          <div className={styles.search}>
            <CatalogueSearch search={active.search} busy={busy} onSearch={onSearch} />
          </div>
          <div className={styles.sort}>
            <CatalogueSort id="catalogue-sort" active={active} busy={busy} onSort={onSort} />
          </div>
        </div>
        <Button
          type="button"
          variant="secondary"
          size="icon"
          className={styles.filterButton}
          aria-label={filterLabel}
          aria-haspopup="dialog"
          aria-expanded={filtersOpen}
          onClick={onOpenFilters}
        >
          <Icon name="filter" />
          {activeFilterCount > 0 && (
            <span className={styles.badge} aria-hidden="true">
              {activeFilterCount}
            </span>
          )}
        </Button>
      </div>
    </div>
  );
}
