import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { Select } from "@/components/ui/Input";
import { CatalogueSearch } from "./CatalogueSearch";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import {
  buildAppliedCataloguePills,
  CATALOGUE_SORT_OPTIONS,
  sortOptionValue,
} from "@/lib/vehicles/catalogue-controls";
import type { CatalogueFilters, CataloguePage } from "@/lib/vehicles/contracts";
import { FEATURED_QUICK_FILTERS, matchingQuickFilterId } from "@/lib/vehicles/featured";
import { catalogueDefaults } from "@/lib/vehicles/query";
import styles from "./Catalogue.module.css";
import layout from "./VehicleLayout.module.css";

interface CatalogueToolbarProps {
  active: CatalogueFilters;
  page: number;
  snapshot: CataloguePage | null;
  branches: Page<Branch> | null;
  busy: boolean;
  onApply: (filters: CatalogueFilters) => void;
  onSearch: (search: string) => void;
  onSort: (sort: CatalogueFilters["sort"], direction: CatalogueFilters["direction"]) => void;
}

export function CatalogueToolbar({
  active,
  page,
  snapshot,
  branches,
  busy,
  onApply,
  onSearch,
  onSort,
}: CatalogueToolbarProps) {
  const activeChipId = matchingQuickFilterId(active);
  const appliedPills = buildAppliedCataloguePills(active, branches);
  const sortValue = sortOptionValue(active);

  const firstItem = snapshot && snapshot.items.length > 0 ? (page - 1) * snapshot.pageSize + 1 : 0;
  const lastItem =
    snapshot && snapshot.items.length > 0 ? firstItem + snapshot.items.length - 1 : 0;

  function handleChipClick(chipId: string, chipFilters: CatalogueFilters) {
    if (chipId !== "all" && activeChipId === chipId) {
      onApply({ ...catalogueDefaults });
      return;
    }
    onApply({ ...chipFilters });
  }

  function handleSortChange(nextValue: string) {
    const selected = CATALOGUE_SORT_OPTIONS.find((option) => option.value === nextValue);
    if (!selected) {
      return;
    }
    onSort(selected.sort, selected.direction);
  }

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
      <CatalogueSearch search={active.search} busy={busy} onSearch={onSearch} />

      <div className={styles.quickChips} role="group" aria-label="Quick vehicle filters">
        {FEATURED_QUICK_FILTERS.map((chip) => {
          const isSelected = activeChipId === chip.id;
          const chipLabel = chip.id === "all" ? "All vehicles" : chip.label;
          return (
            <Button
              key={chip.id}
              type="button"
              variant="secondary"
              size="compact"
              className={styles.quickChip}
              aria-pressed={isSelected}
              disabled={busy}
              onClick={() => handleChipClick(chip.id, chip.filters)}
            >
              {chipLabel}
            </Button>
          );
        })}
      </div>

      <div className={styles.resultsToolbar}>
        <p className={styles.summary} aria-live="polite">
          {renderSummary()}
        </p>

        <div className={styles.sortField}>
          <span className={`${styles.sortLabel} ${layout.fieldLabel}`} aria-hidden="true">
            Sort by
          </span>
          <div className={styles.sortSelect}>
            <Select
              id="catalogue-sort"
              label="Sort by"
              labelHidden
              density="compact"
              value={sortValue}
              disabled={busy}
              onChange={(event) => handleSortChange(event.target.value)}
            >
              {CATALOGUE_SORT_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </Select>
          </div>
        </div>
      </div>

      {appliedPills.length > 0 && (
        <div className={styles.appliedBar} role="group" aria-label="Applied vehicle filters">
          {appliedPills.map((pill) => (
            <Button
              key={pill.key}
              type="button"
              variant="ghost"
              size="row"
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
            size="row"
            className={styles.clearAllButton}
            disabled={busy}
            onClick={() => onApply({ ...catalogueDefaults })}
          >
            Clear all
          </Button>
        </div>
      )}
    </div>
  );
}
