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
import { catalogueBudgetPresets } from "@/lib/vehicles/featured";
import { catalogueDefaults } from "@/lib/vehicles/query";
import styles from "./Catalogue.module.css";
import layout from "./VehicleLayout.module.css";

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
            <h2 className={layout.cardHeading}>Filter vehicles</h2>
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

          <div className={`${styles.fields} ${layout.calmFields}`}>
            <div className={styles.firstGroup}>
              <span className={styles.groupHeading}>Vehicle</span>
              {taxonomy ? (
                <>
                  <Select
                    label="Make"
                    name="make"
                    value={draft.make}
                    onChange={(event) =>
                      setDraft({ ...draft, make: event.target.value, model: "" })
                    }
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
            </div>

            <div className={styles.filterGroup}>
              <span className={styles.groupHeading}>Budget (RM)</span>
              <div className={styles.budgetChips} role="group" aria-label="Quick budget presets">
                {catalogueBudgetPresets.map((preset) => {
                  const activePreset =
                    draft.minimumPrice === preset.minimumPrice &&
                    draft.maximumPrice === preset.maximumPrice;
                  return (
                    <button
                      key={preset.id}
                      type="button"
                      className={styles.filterChip}
                      aria-label={preset.label}
                      aria-pressed={activePreset}
                      onClick={() =>
                        setDraft(
                          activePreset
                            ? { ...draft, minimumPrice: "", maximumPrice: "" }
                            : {
                                ...draft,
                                minimumPrice: preset.minimumPrice,
                                maximumPrice: preset.maximumPrice,
                              },
                        )
                      }
                    >
                      {preset.shortLabel}
                    </button>
                  );
                })}
              </div>
              <div className={styles.rangePair}>
                <Input
                  label="Min (RM)"
                  name="minimumPrice"
                  placeholder="Any"
                  inputMode="decimal"
                  value={draft.minimumPrice}
                  onChange={(event) => change("minimumPrice", event.target.value)}
                />
                <Input
                  label="Max (RM)"
                  name="maximumPrice"
                  placeholder="Any"
                  inputMode="decimal"
                  value={draft.maximumPrice}
                  onChange={(event) => change("maximumPrice", event.target.value)}
                />
              </div>
            </div>

            <div className={styles.filterGroup}>
              <span className={styles.groupHeading}>Manufacture year</span>
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
            </div>

            <div className={styles.filterGroup}>
              <span className={styles.groupHeading}>Showroom</span>
              <BranchFilter
                branches={branches}
                branchId={draft.branchId}
                disabled={branchBusy}
                label="Showroom"
                density="default"
                emptyLabel="All showrooms"
                onChange={(value) => change("branchId", value)}
                onPage={loadBranches}
              />
            </div>

            <div className={styles.filterGroup}>
              <span className={styles.groupHeading}>Transmission</span>
              <div className={styles.segmentedGroup} role="group" aria-label="Transmission">
                {[
                  { value: "", label: "All" },
                  ...transmissions.map((value) => ({ value, label: value })),
                ].map((option) => (
                  <button
                    key={option.label}
                    type="button"
                    className={styles.segmentButton}
                    aria-pressed={draft.transmission === option.value}
                    onClick={() => change("transmission", option.value)}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            </div>

            <div className={styles.filterGroup}>
              <span className={styles.groupHeading}>Fuel type</span>
              <div className={styles.pillWrap} role="group" aria-label="Fuel type">
                <button
                  type="button"
                  className={styles.filterChip}
                  aria-pressed={draft.fuel === ""}
                  onClick={() => change("fuel", "")}
                >
                  All
                </button>
                {fuels.map((value) => (
                  <button
                    key={value}
                    type="button"
                    className={styles.filterChip}
                    aria-pressed={draft.fuel === value}
                    onClick={() => change("fuel", draft.fuel === value ? "" : value)}
                  >
                    {fuelLabel(value)}
                  </button>
                ))}
              </div>
            </div>
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
