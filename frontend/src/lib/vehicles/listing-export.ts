import { privateApiFetch } from "@/lib/api-browser";
import { downloadCsv, selectedCsv, type CsvColumn } from "@/lib/csv";
import {
  listingPublication,
  listingQuery,
  type ListingFilters,
  type ListingItem,
} from "./listings";
import { parseListings } from "./parse-listings";

export const listingExportColumns: readonly CsvColumn<ListingItem>[] = [
  { label: "Registration", value: (row) => row.registrationPlate },
  { label: "Make", value: (row) => row.make },
  { label: "Model", value: (row) => row.model },
  { label: "Trim", value: (row) => row.trim },
  { label: "Year", value: (row) => String(row.manufactureYear) },
  { label: "Mileage (km)", value: (row) => String(row.mileageKilometres) },
  { label: "Branch", value: (row) => row.branchName },
  { label: "Inspection status", value: (row) => row.vehicleStatus },
  { label: "Draft price (MYR)", value: (row) => row.vehiclePrice?.toFixed(2) ?? "" },
  { label: "Publication", value: listingPublication },
];

export async function exportListings(filters: ListingFilters, cancellation: AbortSignal) {
  const signal = AbortSignal.any([cancellation, AbortSignal.timeout(120_000)]);
  const rows: ListingItem[] = [];
  const ids = new Set<string>();
  let total: number | null = null;
  let page = 1;
  while (total === null || rows.length < total) {
    const result = await privateApiFetch(
      `/api/staff/listings?${listingQuery({ ...filters, pageSize: 50 }, page)}`,
      parseListings,
      { signal },
    );
    const snapshot = result.data;
    if (
      !snapshot ||
      snapshot.page !== page ||
      snapshot.pageSize !== 50 ||
      snapshot.total > 10_000
    ) {
      throw new Error(
        "The listing export could not be confirmed or exceeds 10,000 rows. Narrow the filters and retry.",
      );
    }
    if (
      (total !== null && total !== snapshot.total) ||
      snapshot.items.length !== Math.min(50, snapshot.total - rows.length)
    ) {
      throw new Error("The listings changed during export. Retry to download the current results.");
    }
    for (const row of snapshot.items) {
      if (ids.has(row.vehicleId)) {
        throw new Error(
          "The listings changed during export. Retry to download the current results.",
        );
      }
      ids.add(row.vehicleId);
      rows.push(row);
    }
    total = snapshot.total;
    page += 1;
  }
  signal.throwIfAborted();
  downloadCsv(
    new Blob([selectedCsv(rows, listingExportColumns)], { type: "text/csv;charset=utf-8" }),
    "listings-full.csv",
  );
}
