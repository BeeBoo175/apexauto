import type { DataColumn } from "@/components/ui/data-table/types";
import type { useListingTable } from "@/hooks/use-listing-table";
import type { AppliedWorklistFilter } from "@/lib/staff/filter-summary";
import {
  listingInspectionOptions,
  type ListingFilters as Filters,
  type ListingItem,
} from "@/lib/vehicles/listings";
import { ListingFilters } from "./ListingFilters";
import { dateRangeLabel } from "@/lib/date-range-label";

export function listingTableFilters(
  state: ReturnType<typeof useListingTable>,
  columns: DataColumn<ListingItem>[],
) {
  const filters = state.active.filters;
  const applied: AppliedWorklistFilter<Filters>[] = filters.branchId
    ? [
        {
          key: "branch",
          label:
            state.branches?.items.find((branch) => branch.id === filters.branchId)?.name ??
            "Selected branch",
          without: { ...filters, branchId: "" },
        },
      ]
    : [];
  for (const filter of [{ key: "inspection", options: listingInspectionOptions }] as const) {
    const value = filters[filter.key];
    if (value) {
      applied.push({
        key: filter.key,
        label: filter.options.find((option) => option.value === value)?.label ?? value,
        without: { ...filters, [filter.key]: "" },
      });
    }
  }
  for (const key of ["registration", "vehicle"] as const) {
    if (filters[key]) {
      applied.push({
        key,
        label: `${key === "registration" ? "Registration" : "Vehicle"}: ${filters[key]}`,
        without: { ...filters, [key]: "" },
      });
    }
  }
  if (filters.from || filters.to) {
    applied.push({
      key: "dates",
      label: `Listing activity: ${dateRangeLabel(filters.from ?? "", filters.to ?? "")}`,
      without: { ...filters, from: "", to: "" },
    });
  }
  for (const range of [
    { key: "price", label: "Draft price", minimum: "minimumPrice", maximum: "maximumPrice" },
    {
      key: "publishedPrice",
      label: "Published price",
      minimum: "minimumPublishedPrice",
      maximum: "maximumPublishedPrice",
    },
  ] as const) {
    const lower = filters[range.minimum];
    const upper = filters[range.maximum];
    if (lower || upper) {
      applied.push({
        key: range.key,
        label: `${range.label}: MYR ${lower || "0"}–${upper || "any"}`,
        without: { ...filters, [range.minimum]: "", [range.maximum]: "" },
      });
    }
  }

  const filteredColumns = columns.map((column) => {
    let activeFilter = Boolean(filters.inspection);
    if (column.id === "registration" || column.id === "vehicle") {
      activeFilter = Boolean(filters[column.id]);
    }
    if (column.id === "publication") {
      activeFilter = Boolean(filters.publication);
    }
    if (column.id === "activity") {
      activeFilter = Boolean(filters.from || filters.to);
    }
    if (column.id === "branch") {
      activeFilter = Boolean(filters.branchId);
    }
    if (column.id === "price") {
      activeFilter = Boolean(filters.minimumPrice || filters.maximumPrice);
    }
    if (column.id === "publishedPrice") {
      activeFilter = Boolean(filters.minimumPublishedPrice || filters.maximumPublishedPrice);
    }
    return {
      ...column,
      filter: {
        active: activeFilter,
        content: <ListingFilters state={state} column={column.id} />,
        onOpenChange: () =>
          state.setDraft((draft) => ({
            ...draft,
            branchId: filters.branchId,
            inspection: filters.inspection,
            publication: filters.publication,
          })),
      },
    };
  });

  return { columns: filteredColumns, applied };
}
