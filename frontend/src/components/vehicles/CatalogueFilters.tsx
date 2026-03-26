import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { BranchFilter } from "@/components/staff/BranchFilter";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import { fuels, transmissions, type CatalogueFilters as Filters } from "@/lib/vehicles/contracts";
import { catalogueDefaults } from "@/lib/vehicles/query";
import styles from "./Catalogue.module.css";

export function CatalogueFilters({
  draft,
  setDraft,
  busy,
  apply,
  branches,
  branchBusy,
  loadBranches,
}: {
  draft: Filters;
  setDraft: (filters: Filters) => void;
  busy: boolean;
  apply: (filters: Filters) => void;
  branches: Page<Branch> | null;
  branchBusy: boolean;
  loadBranches: (page: number) => void;
}) {
  const change = (key: keyof Filters, value: string) => setDraft({ ...draft, [key]: value });
  return (
    <form
      className={styles.filters}
      method="get"
      action="/vehicles"
      onSubmit={(event) => {
        event.preventDefault();
        apply(draft);
      }}
    >
      <div className={styles.fields}>
        <Input
          label="Make"
          name="make"
          value={draft.make}
          maxLength={100}
          onChange={(event) => change("make", event.target.value)}
        />
        <Input
          label="Model"
          name="model"
          value={draft.model}
          maxLength={100}
          onChange={(event) => change("model", event.target.value)}
        />
        <Input
          label="Minimum year"
          name="minimumYear"
          inputMode="numeric"
          value={draft.minimumYear}
          maxLength={4}
          onChange={(event) => change("minimumYear", event.target.value)}
        />
        <Input
          label="Maximum year"
          name="maximumYear"
          inputMode="numeric"
          value={draft.maximumYear}
          maxLength={4}
          onChange={(event) => change("maximumYear", event.target.value)}
        />
        <Input
          label="Minimum price (MYR)"
          name="minimumPrice"
          inputMode="decimal"
          value={draft.minimumPrice}
          onChange={(event) => change("minimumPrice", event.target.value)}
        />
        <Input
          label="Maximum price (MYR)"
          name="maximumPrice"
          inputMode="decimal"
          value={draft.maximumPrice}
          onChange={(event) => change("maximumPrice", event.target.value)}
        />
        <Select
          label="Transmission"
          name="transmission"
          value={draft.transmission}
          onChange={(event) => change("transmission", event.target.value)}
        >
          <option value="">All transmissions</option>
          {transmissions.map((value) => (
            <option key={value}>{value}</option>
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
              {value === "PlugInHybrid" ? "Plug-in hybrid" : value}
            </option>
          ))}
        </Select>
        <BranchFilter
          branches={branches}
          branchId={draft.branchId}
          disabled={branchBusy}
          labelHidden={false}
          density="default"
          onChange={(value) => change("branchId", value)}
          onPage={loadBranches}
        />
        <Select
          label="Sort by"
          name="sort"
          value={draft.sort}
          onChange={(event) => change("sort", event.target.value)}
        >
          <option value="published">Recently published</option>
          <option value="price">Vehicle price</option>
          <option value="year">Manufacture year</option>
          <option value="mileage">Mileage</option>
        </Select>
        <Select
          label="Order"
          name="direction"
          value={draft.direction}
          onChange={(event) => change("direction", event.target.value)}
        >
          <option value="desc">Highest / newest first</option>
          <option value="asc">Lowest / oldest first</option>
        </Select>
      </div>
      <div className={styles.actions}>
        <Button type="submit" disabled={busy}>
          Apply filters
        </Button>
        <Button
          type="button"
          variant="secondary"
          disabled={busy}
          onClick={() => {
            setDraft({ ...catalogueDefaults });
            apply({ ...catalogueDefaults });
          }}
        >
          Clear filters
        </Button>
      </div>
    </form>
  );
}
