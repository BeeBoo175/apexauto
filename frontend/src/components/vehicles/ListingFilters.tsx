import { BranchFilter } from "@/components/staff/BranchFilter";
import { FilterChoices } from "@/components/ui/FilterChoices";
import {
  ColumnFilterForm,
  DateRangeFilter,
  MoneyRangeFilter,
  TextColumnFilter,
} from "@/components/ui/data-table/ColumnFilterForm";
import { listingInspectionOptions, listingPublicationOptions } from "@/lib/vehicles/listings";
import type { useListingTable } from "@/hooks/use-listing-table";

export function ListingFilters({
  state,
  column,
}: {
  state: ReturnType<typeof useListingTable>;
  column: string;
}) {
  const active = state.active.filters;
  if (column === "registration" || column === "vehicle") {
    return (
      <TextColumnFilter
        label={column === "registration" ? "Registration contains" : "Vehicle contains"}
        value={active[column] ?? ""}
        busy={state.busy}
        onApply={(value) => state.load({ ...active, [column]: value }, 1)}
      />
    );
  }
  if (column === "activity") {
    return (
      <DateRangeFilter
        from={active.from ?? ""}
        to={active.to ?? ""}
        busy={state.busy}
        onApply={(from, to) => state.load({ ...active, from, to }, 1)}
      />
    );
  }
  if (column === "price" || column === "publishedPrice") {
    const minimum = column === "price" ? "minimumPrice" : "minimumPublishedPrice";
    const maximum = column === "price" ? "maximumPrice" : "maximumPublishedPrice";
    return (
      <MoneyRangeFilter
        minimum={active[minimum] ?? ""}
        maximum={active[maximum] ?? ""}
        busy={state.busy}
        onApply={(lower, upper) => state.load({ ...active, [minimum]: lower, [maximum]: upper }, 1)}
      />
    );
  }
  const branch = column === "branch";
  let key: "branchId" | "inspection" | "publication" = "inspection";
  if (branch) {
    key = "branchId";
  }
  if (column === "publication") {
    key = "publication";
  }
  return (
    <ColumnFilterForm
      busy={state.busy}
      onApply={() => state.load({ ...active, [key]: state.draft[key] }, 1)}
      onClear={() => state.load({ ...active, [key]: "" }, 1, true)}
    >
      {branch ? (
        <BranchFilter
          branches={state.branches}
          branchId={state.draft.branchId}
          disabled={state.busy}
          labelHidden
          onChange={(branchId) => state.setDraft({ ...state.draft, branchId })}
          onPage={(page) => void state.loadBranches(page)}
        />
      ) : (
        <FilterChoices
          label={column === "publication" ? "Publication queue" : "Inspection"}
          options={column === "publication" ? listingPublicationOptions : listingInspectionOptions}
          value={state.draft[key] ?? ""}
          disabled={state.busy}
          onChange={(value) => state.setDraft({ ...state.draft, [key]: value })}
        />
      )}
    </ColumnFilterForm>
  );
}
