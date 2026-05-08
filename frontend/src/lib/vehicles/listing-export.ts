import { privateApiFetch } from "@/lib/api-browser";
import { exportPagedCsv, type CsvColumn } from "@/lib/csv";
import {
  listingPublication,
  listingQuery,
  type ListingFilters,
  type ListingItem,
} from "./listings";
import { parseListings } from "./parse-listings";
import { listingActivity, listingInspectionLabel, listingVisibilityNote } from "./listing-table";
import { formatMalaysianDateTime } from "@/lib/date-format";

export const listingExportColumns: readonly CsvColumn<ListingItem>[] = [
  { label: "Registration", value: (row) => row.registrationPlate },
  { label: "Make", value: (row) => row.make },
  { label: "Model", value: (row) => row.model },
  { label: "Trim", value: (row) => row.trim },
  { label: "Year", value: (row) => String(row.manufactureYear) },
  { label: "Mileage (km)", value: (row) => String(row.mileageKilometres) },
  { label: "Branch", value: (row) => row.branchName },
  { label: "Inspection", value: listingInspectionLabel },
  {
    label: "Approved vehicle price (MYR)",
    value: (row) => row.approvedVehiclePrice?.toFixed(2) ?? "",
  },
  { label: "Draft price (MYR)", value: (row) => row.vehiclePrice?.toFixed(2) ?? "" },
  { label: "Publication", value: listingPublication },
  { label: "Visible on public site", value: (row) => (row.isPublic ? "Yes" : "No") },
  { label: "Publication context", value: listingVisibilityNote },
  {
    label: "Listing activity",
    value: (row) => {
      const activity = listingActivity(row);
      return activity ? `${activity.label} · ${formatMalaysianDateTime(activity.at)}` : "";
    },
  },
];

export function exportListings(filters: ListingFilters, cancellation: AbortSignal) {
  return exportPagedCsv(
    {
      read: (page, pageSize, signal) =>
        privateApiFetch(
          `/api/staff/listings?${listingQuery({ ...filters, pageSize }, page)}`,
          parseListings,
          { signal },
        ),
      id: (row) => row.vehicleId,
      columns: listingExportColumns,
      filename: "listings-full.csv",
      unavailable:
        "The listing export could not be confirmed or exceeds 10,000 rows. Narrow the filters and retry.",
      changed: "The listings changed during export. Retry to download the current results.",
    },
    cancellation,
  );
}
