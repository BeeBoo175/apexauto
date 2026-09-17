import { Select } from "@/components/ui/Input";
import { CATALOGUE_SORT_OPTIONS, sortOptionValue } from "@/lib/vehicles/catalogue-controls";
import type { CatalogueFilters } from "@/lib/vehicles/contracts";

interface CatalogueSortProps {
  id: string;
  active: CatalogueFilters;
  busy: boolean;
  onSort: (sort: CatalogueFilters["sort"], direction: CatalogueFilters["direction"]) => void;
}

export function CatalogueSort({ id, active, busy, onSort }: CatalogueSortProps) {
  function handleChange(nextValue: string) {
    const selected = CATALOGUE_SORT_OPTIONS.find((option) => option.value === nextValue);
    if (!selected) {
      return;
    }
    onSort(selected.sort, selected.direction);
  }

  return (
    <Select
      id={id}
      label="Sort by"
      labelHidden
      value={sortOptionValue(active)}
      disabled={busy}
      onChange={(event) => handleChange(event.target.value)}
    >
      {CATALOGUE_SORT_OPTIONS.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </Select>
  );
}
