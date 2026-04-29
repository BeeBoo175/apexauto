import { privateApiFetch } from "@/lib/api-browser";
import { downloadCsv, selectedCsv, type CsvColumn } from "@/lib/csv";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { parseReservationSnapshot } from "./parse-reservations";
import {
  reservationQuery,
  reservationStatuses,
  type ReservationFilters,
  type ReservationItem,
} from "./reservations";

export const reservationExportColumns: readonly CsvColumn<ReservationItem>[] = [
  { label: "Registration", value: (row) => row.registrationPlate },
  { label: "Vehicle", value: (row) => row.vehicle },
  { label: "Status", value: (row) => reservationStatuses[row.status] },
  { label: "Buyer", value: (row) => row.buyer },
  { label: "Handled by", value: (row) => row.claim?.agent ?? "" },
  {
    label: "Claimed (Malaysia)",
    value: (row) => (row.claim ? formatMalaysianDateTime(row.claim.claimedAtUtc) : ""),
  },
  { label: "Reserved total (MYR)", value: (row) => row.total.toFixed(2) },
  { label: "Expires (Malaysia)", value: (row) => formatMalaysianDateTime(row.expiresAt) },
  {
    label: "Released (Malaysia)",
    value: (row) => (row.releasedAt ? formatMalaysianDateTime(row.releasedAt) : ""),
  },
  { label: "Created (Malaysia)", value: (row) => formatMalaysianDateTime(row.createdAt) },
  { label: "Branch", value: (row) => row.branchName },
  { label: "Reference", value: (row) => row.number },
];

export async function exportReservations(filters: ReservationFilters, cancellation: AbortSignal) {
  const signal = AbortSignal.any([cancellation, AbortSignal.timeout(120_000)]);
  const rows: ReservationItem[] = [];
  const ids = new Set<string>();
  let total: number | null = null;
  let page = 1;
  while (total === null || rows.length < total) {
    const result = await privateApiFetch(
      `/api/staff/reservations?${reservationQuery({ ...filters, pageSize: 50 }, page)}`,
      parseReservationSnapshot,
      { signal },
    );
    const snapshot = result.data;
    if (
      result.status !== 200 ||
      !snapshot ||
      snapshot.page !== page ||
      snapshot.pageSize !== 50 ||
      snapshot.total > 10_000
    ) {
      throw new Error(
        "Reservation export could not be confirmed or exceeds 10,000 rows. Narrow the filters and retry.",
      );
    }
    if (
      (total !== null && total !== snapshot.total) ||
      snapshot.items.length !== Math.min(50, snapshot.total - rows.length)
    ) {
      throw new Error("Reservations changed during export. Retry to download the current results.");
    }
    for (const row of snapshot.items) {
      if (ids.has(row.id)) {
        throw new Error(
          "Reservations changed during export. Retry to download the current results.",
        );
      }
      ids.add(row.id);
      rows.push(row);
    }
    total = snapshot.total;
    page += 1;
  }
  signal.throwIfAborted();
  downloadCsv(
    new Blob([selectedCsv(rows, reservationExportColumns)], { type: "text/csv;charset=utf-8" }),
    "reservations-full.csv",
  );
}
