import { BranchFilter } from "@/components/staff/BranchFilter";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { Input, Select } from "@/components/ui/Input";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { Taxonomy } from "@/lib/intake/contracts";
import {
  fuels,
  fuelLabel,
  transmissions,
  type CatalogueFilters as Filters,
} from "@/lib/vehicles/contracts";
import { countActiveCatalogueFilters } from "@/lib/vehicles/catalogue-controls";
import { catalogueDefaults } from "@/lib/vehicles/query";
import styles from "./Catalogue.module.css";

interface CatalogueFiltersProps {
  active: Filters;
  draft: Filters;
  setDraft: (filters: Filters) => void;
  busy: boolean;
  apply: (filters: Filters) => void;
  branches: Page<Branch> | null;
  branchBusy: boolean;
  loadBranches: (page: number) => void;
  taxonomy: Taxonomy | null;
  mobileOpen: boolean;
  onToggleMobile: () => void;
}

export function CatalogueFilters({
  active,
  draft,
  setDraft,
  busy,
  apply,
  branches,
  branchBusy,
  loadBranches,
  taxonomy,
  mobileOpen,
  onToggleMobile,
}: CatalogueFiltersProps) {
  const activeCount = countActiveCatalogueFilters(active);
  const draftCount = countActiveCatalogueFilters(draft);
  const selectedMake = taxonomy?.makes.find((item) => item.name === draft.make);
  const customMake = taxonomy && draft.make && !selectedMake ? draft.make : null;
  const availableModels = selectedMake?.models ?? [];
  const customModel =
    taxonomy && draft.model && !availableModels.some((item) => item.name === draft.model)
      ? draft.model
      : null;

  function change(key: keyof Filters, value: string) {
    setDraft({ ...draft, [key]: value });
  }

  function handleReset() {
    const resetFilters = {
      ...catalogueDefaults,
      sort: active.sort,
      direction: active.direction,
    };
    if (activeCount === 0) {
      setDraft(resetFilters);
      return;
    }
    apply(resetFilters);
  }

  return (
    <aside className={styles.filterRail} aria-label="Filter inventory">
      <div className={styles.mobileToggleBar}>
        <Button
          type="button"
          variant="secondary"
          size="md"
          className={styles.mobileToggle}
          aria-expanded={mobileOpen}
          aria-controls="catalogue-filter-panel"
          onClick={onToggleMobile}
        >
          <span className={styles.mobileToggleLabel}>
            <Icon name="filter" />
            <span>Filter vehicles</span>
            {activeCount > 0 && (
              <span className={styles.filterCountBadge}>
                {activeCount}
                <span className="sr-only">
                  {" "}
                  {activeCount === 1 ? "active filter" : "active filters"}
                </span>
              </span>
            )}
          </span>
          <span className={styles.mobileChevron} data-open={mobileOpen || undefined}>
            <Icon name="down" />
          </span>
        </Button>
      </div>

      <div id="catalogue-filter-panel" className={styles.filterPanel} data-open={mobileOpen}>
        <form
          className={styles.filters}
          method="get"
          action="/vehicles"
          onSubmit={(event) => {
            event.preventDefault();
            apply(draft);
          }}
        >
          <div className={styles.railHeader}>
            <h2>Filter vehicles</h2>
            {(draftCount > 0 || activeCount > 0) && (
              <Button
                type="button"
                variant="ghost"
                size="row"
                disabled={busy}
                onClick={handleReset}
              >
                Reset
              </Button>
            )}
          </div>

          <div className={styles.fields}>
            {taxonomy ? (
              <>
                <Select
                  label="Make"
                  name="make"
                  value={draft.make}
                  onChange={(event) => setDraft({ ...draft, make: event.target.value, model: "" })}
                >
                  <option value="">All makes</option>
                  {customMake && <option value={customMake}>{customMake}</option>}
                  {taxonomy.makes.map((item) => (
                    <option key={item.name} value={item.name}>
                      {item.name}
                    </option>
                  ))}
                </Select>
                <Select
                  label="Model"
                  name="model"
                  value={draft.model}
                  disabled={!draft.make && !draft.model}
                  onChange={(event) => change("model", event.target.value)}
                >
                  <option value="">All models</option>
                  {customModel && <option value={customModel}>{customModel}</option>}
                  {availableModels.map((item) => (
                    <option key={item.name} value={item.name}>
                      {item.name}
                    </option>
                  ))}
                </Select>
              </>
            ) : (
              <>
                <Input
                  label="Make"
                  name="make"
                  placeholder="e.g. Honda"
                  value={draft.make}
                  maxLength={100}
                  onChange={(event) => change("make", event.target.value)}
                />
                <Input
                  label="Model"
                  name="model"
                  placeholder="e.g. City"
                  value={draft.model}
                  maxLength={100}
                  onChange={(event) => change("model", event.target.value)}
                />
              </>
            )}

            <div className={styles.rangePair}>
              <Input
                label="Min (MYR)"
                name="minimumPrice"
                placeholder="Any"
                inputMode="decimal"
                value={draft.minimumPrice}
                onChange={(event) => change("minimumPrice", event.target.value)}
              />
              <Input
                label="Max (MYR)"
                name="maximumPrice"
                placeholder="Any"
                inputMode="decimal"
                value={draft.maximumPrice}
                onChange={(event) => change("maximumPrice", event.target.value)}
              />
            </div>

            <div className={styles.rangePair}>
              <Input
                label="Min year"
                name="minimumYear"
                placeholder="Any"
                inputMode="numeric"
                value={draft.minimumYear}
                maxLength={4}
                onChange={(event) => change("minimumYear", event.target.value)}
              />
              <Input
                label="Max year"
                name="maximumYear"
                placeholder="Any"
                inputMode="numeric"
                value={draft.maximumYear}
                maxLength={4}
                onChange={(event) => change("maximumYear", event.target.value)}
              />
            </div>

            <BranchFilter
              branches={branches}
              branchId={draft.branchId}
              disabled={branchBusy}
              label="Showroom"
              labelHidden={false}
              density="default"
              emptyLabel="All showrooms"
              onChange={(value) => change("branchId", value)}
              onPage={loadBranches}
            />

            <Select
              label="Transmission"
              name="transmission"
              value={draft.transmission}
              onChange={(event) => change("transmission", event.target.value)}
            >
              <option value="">All transmissions</option>
              {transmissions.map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </Select>

            <Select
              label="Fuel"
              name="fuel"
              value={draft.fuel}
              onChange={(event) => change("fuel", event.target.value)}
            >
              <option value="">All fuels</option>
              {fuels.map((value) => (
                <option key={value} value={value}>
                  {fuelLabel(value)}
                </option>
              ))}
            </Select>
          </div>

          <div className={styles.railActions}>
            <Button type="submit" size="md" className={styles.applyButton} disabled={busy}>
              Apply filters
            </Button>
          </div>
        </form>
      </div>
    </aside>
  );
}
