import { privateApiFetch } from "@/lib/api-browser";
import { exportPagedCsv, type CsvColumn } from "@/lib/csv";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { testDriveStatusLabels } from "./account";
import { parseStaffTestDrivePage } from "./staff";
import { staffTestDriveQuery, type StaffTestDriveItem, type TestDriveFilters } from "./staff-query";

export function testDriveExportColumns(group: boolean): CsvColumn<StaffTestDriveItem>[] {
  const columns: CsvColumn<StaffTestDriveItem>[] = [
    { label: "Reference", value: (row) => row.id },
    { label: "Registration", value: (row) => row.registrationPlate },
    {
      label: "Vehicle",
      value: (row) => `${row.manufactureYear} ${row.make} ${row.model} ${row.trim}`,
    },
    { label: "Customer", value: (row) => row.customerName },
    { label: "Handling staff", value: (row) => row.handlerEmail ?? "Unassigned" },
    { label: "Status", value: (row) => testDriveStatusLabels[row.status] },
    { label: "Booked start (Malaysia)", value: (row) => formatMalaysianDateTime(row.startsAt) },
    { label: "Booked end (Malaysia)", value: (row) => formatMalaysianDateTime(row.endsAt) },
  ];
  if (group) {
    columns.push({ label: "Branch", value: (row) => row.branchName });
  }
  return columns;
}

export function exportTestDrives(filters: TestDriveFilters, group: boolean, signal: AbortSignal) {
  return exportPagedCsv(
    {
      read: (page, pageSize, requestSignal) =>
        privateApiFetch(
          `/api/staff/test-drives?${staffTestDriveQuery({ ...filters, pageSize }, page)}`,
          parseStaffTestDrivePage,
          { signal: requestSignal },
        ),
      id: (row) => row.id,
      columns: testDriveExportColumns(group),
      filename: "test-drives.csv",
      unavailable:
        "Test drives could not be exported. Narrow the filters to at most 10,000 records and retry.",
      changed: "Test drives changed during export. Refresh the list and retry.",
    },
    signal,
  );
}
