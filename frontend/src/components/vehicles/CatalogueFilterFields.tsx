import { BranchFilter } from "@/components/staff/BranchFilter";
import { Input, Select } from "@/components/ui/Input";
import { SegmentedControl } from "@/components/ui/SegmentedControl";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { Taxonomy } from "@/lib/intake/contracts";
import {
  fuels,
  fuelLabel,
  parseGradeList,
  transmissions,
  vehicleGrades,
  type CatalogueFilters as Filters,
  type VehicleGrade,
} from "@/lib/vehicles/contracts";
import { catalogueBudgetPresets } from "@/lib/vehicles/featured";
import styles from "./CatalogueFilters.module.css";
import layout from "./VehicleLayout.module.css";

const transmissionOptions = [
  { value: "", label: "All" },
  ...transmissions.map((value) => ({ value, label: value })),
];

interface CatalogueFilterFieldsProps {
  active: Filters;
  draft: Filters;
  setDraft: (filters: Filters) => void;
  apply: (filters: Filters) => void;
  branches: Page<Branch> | null;
  branchBusy: boolean;
  loadBranches: (page: number) => void;
  taxonomy: Taxonomy | null;
  // The hero's showroom tabs already cover the showroom choice when every branch fits there.
  showroomInTabs: boolean;
  // Phones sort from inside the drawer, because the control bar has no room for it.
  mobileSort: React.ReactNode;
}

export function CatalogueFilterFields({
  active,
  draft,
  setDraft,
  apply,
  branches,
  branchBusy,
  loadBranches,
  taxonomy,
  showroomInTabs,
  mobileSort,
}: CatalogueFilterFieldsProps) {
  const selectedMake = taxonomy?.makes.find((item) => item.name === draft.make);
  const customMake = taxonomy && draft.make && !selectedMake ? draft.make : null;
  const availableModels = selectedMake?.models ?? [];
  const customModel =
    taxonomy && draft.model && !availableModels.some((item) => item.name === draft.model)
      ? draft.model
      : null;

  // Choosing from a list applies at once; typed values apply when the field is left or Enter is pressed.
  function commit(next: Filters) {
    setDraft(next);
    apply(next);
  }

  function change(key: keyof Filters, value: string) {
    commit({ ...draft, [key]: value });
  }

  // Grades toggle on and off independently; none chosen means every grade.
  const selectedGrades = parseGradeList(draft.grade) ?? [];

  function toggleGrade(grade: VehicleGrade) {
    const next = selectedGrades.includes(grade)
      ? selectedGrades.filter((item) => item !== grade)
      : vehicleGrades.filter((item) => item === grade || selectedGrades.includes(item));
    change("grade", next.join(","));
  }

  function type(key: keyof Filters, value: string) {
    setDraft({ ...draft, [key]: value });
  }

  function commitTyped() {
    const typedKeys = [
      "make",
      "model",
      "minimumPrice",
      "maximumPrice",
      "minimumYear",
      "maximumYear",
    ] as const;
    if (typedKeys.some((key) => draft[key] !== active[key])) {
      apply(draft);
    }
  }

  return (
    <div className={`${styles.fields} ${layout.calmFields}`}>
      <div className={`${styles.filterGroup} ${styles.sortGroup}`}>
        <span className={styles.groupHeading}>Sort by</span>
        {mobileSort}
      </div>

      <div className={styles.firstGroup}>
        <span className={styles.groupHeading}>Vehicle</span>
        {taxonomy ? (
          <>
            <Select
              label="Make"
              labelHidden
              name="make"
              value={draft.make}
              onChange={(event) => commit({ ...draft, make: event.target.value, model: "" })}
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
              labelHidden
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
              labelHidden
              name="make"
              placeholder="e.g. Honda"
              value={draft.make}
              maxLength={100}
              onChange={(event) => type("make", event.target.value)}
              onBlur={commitTyped}
            />
            <Input
              label="Model"
              labelHidden
              name="model"
              placeholder="e.g. City"
              value={draft.model}
              maxLength={100}
              onChange={(event) => type("model", event.target.value)}
              onBlur={commitTyped}
            />
          </>
        )}
      </div>

      <div className={styles.filterGroup}>
        <span className={styles.groupHeading}>Budget (RM)</span>
        <SegmentedControl
          fill
          label="Quick budget presets"
          options={catalogueBudgetPresets.map((preset) => {
            const activePreset =
              draft.minimumPrice === preset.minimumPrice &&
              draft.maximumPrice === preset.maximumPrice;
            return {
              key: preset.id,
              label: preset.shortLabel,
              ariaLabel: preset.label,
              selected: activePreset,
              onSelect: () =>
                commit(
                  activePreset
                    ? { ...draft, minimumPrice: "", maximumPrice: "" }
                    : {
                        ...draft,
                        minimumPrice: preset.minimumPrice,
                        maximumPrice: preset.maximumPrice,
                      },
                ),
            };
          })}
        />
        <div className={styles.rangePair}>
          <Input
            label="Minimum price (RM)"
            labelHidden
            name="minimumPrice"
            placeholder="Min"
            inputMode="decimal"
            value={draft.minimumPrice}
            onChange={(event) => type("minimumPrice", event.target.value)}
            onBlur={commitTyped}
          />
          <Input
            label="Maximum price (RM)"
            labelHidden
            name="maximumPrice"
            placeholder="Max"
            inputMode="decimal"
            value={draft.maximumPrice}
            onChange={(event) => type("maximumPrice", event.target.value)}
            onBlur={commitTyped}
          />
        </div>
      </div>

      <div className={styles.filterGroup}>
        <span className={styles.groupHeading}>Manufacture year</span>
        <div className={styles.rangePair}>
          <Input
            label="Minimum year"
            labelHidden
            name="minimumYear"
            placeholder="From"
            inputMode="numeric"
            value={draft.minimumYear}
            maxLength={4}
            onChange={(event) => type("minimumYear", event.target.value)}
            onBlur={commitTyped}
          />
          <Input
            label="Maximum year"
            labelHidden
            name="maximumYear"
            placeholder="To"
            inputMode="numeric"
            value={draft.maximumYear}
            maxLength={4}
            onChange={(event) => type("maximumYear", event.target.value)}
            onBlur={commitTyped}
          />
        </div>
      </div>

      {!showroomInTabs && (
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
      )}

      <div className={styles.filterGroup}>
        <span className={styles.groupHeading}>Inspection grade</span>
        <SegmentedControl
          fill
          label="Inspection grade"
          options={vehicleGrades.map((grade) => ({
            key: grade,
            label: `Grade ${grade}`,
            selected: selectedGrades.includes(grade),
            onSelect: () => toggleGrade(grade),
          }))}
        />
      </div>

      <div className={styles.filterGroup}>
        <span className={styles.groupHeading}>Transmission</span>
        <SegmentedControl
          fill
          label="Transmission"
          options={transmissionOptions.map((option) => ({
            key: option.label,
            label: option.label,
            selected: draft.transmission === option.value,
            onSelect: () => change("transmission", option.value),
          }))}
        />
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
  );
}
