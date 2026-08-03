"use client";

import { Button } from "@/components/ui/Button";
import { SearchField } from "@/components/ui/SearchField";
import { RecordToolbar } from "@/components/ui/RecordToolbar";
import { CsvExportActions } from "@/components/ui/CsvExportActions";
import { DataTable } from "@/components/ui/data-table/DataTable";
import type { DataColumn } from "@/components/ui/data-table/types";
import { HandlerAvatar } from "@/components/staff/HandlerAvatar";
import { SuspendedPerson } from "@/components/staff/SuspensionBadge";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { VehicleDescription, VehicleRegistration } from "@/components/staff/VehicleTableCells";
import { staffColumnSizing } from "@/lib/staff/table-columns";
import { AppliedWorklistFilters } from "@/components/staff/AppliedWorklistFilters";
import { useRowSelection } from "@/hooks/use-row-selection";
import { useCsvExport } from "@/hooks/use-csv-export";
import type { useStaffTestDriveList } from "@/hooks/use-staff-test-drive-list";
import {
  testDriveSections,
  staffTestDriveQuery,
  type StaffTestDriveItem,
  type TestDriveFilters,
} from "@/lib/test-drives/staff-query";
import { exportTestDrives, testDriveExportColumns } from "@/lib/test-drives/staff-export";
import type { AppliedFilter } from "@/lib/staff/filter-summary";
import type { Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { StaffTestDriveStatus } from "./StaffTestDriveStatus";
import { StaffTestDriveFilters } from "./StaffTestDriveFilters";
import styles from "@/components/staff/StaffWorklist.module.css";
import table from "@/components/staff/StaffTable.module.css";

export function StaffTestDriveTable({
  list,
  group,
  branches,
  selectedId,
  open,
}: {
  list: ReturnType<typeof useStaffTestDriveList>;
  group: boolean;
  branches: Page<Branch> | null;
  selectedId: string;
  open: (id: string) => void;
}) {
  const { snapshot, active, busy, draft, setDraft } = list;
  const filters = active.filters;
  const selection = useRowSelection(
    snapshot.items,
    (row) => row.id,
    `${staffTestDriveQuery(filters, active.page)}:${snapshot.actor.userId}:${list.revision}`,
  );
  const csv = useCsvExport(testDriveExportColumns(group), "test-drives", (signal) =>
    exportTestDrives(filters, group, signal),
  );
  const disabled = Boolean(selectedId) || csv.busy;
  const indicators: Record<string, boolean> = {
    registrationPlate: Boolean(filters.registration),
    vehicle: Boolean(filters.vehicle),
    customerName: Boolean(filters.customer),
    status: filters.status !== "all",
    startsAt: Boolean(filters.from || filters.to),
    handlerEmail: Boolean(filters.handlerId || filters.handler),
  };
  if (group) {
    indicators.branchName = Boolean(filters.branchId);
  }
  const columns: DataColumn<StaffTestDriveItem>[] = [
    {
      id: "registrationPlate",
      label: "Registration",
      ...staffColumnSizing.registration,
      sortable: true,
      cell: (row) => <VehicleRegistration registration={row.registrationPlate} />,
    },
    {
      id: "vehicle",
      label: "Vehicle",
      ...staffColumnSizing.vehicle,
      sortable: true,
      cell: (row) => <VehicleDescription vehicle={row} />,
    },
    {
      id: "customerName",
      label: "Customer",
      ...staffColumnSizing.person,
      sortable: true,
      cell: (row) => (
        <SuspendedPerson person="Customer" suspended={row.customerSuspended}>
          {row.customerName}
        </SuspendedPerson>
      ),
    },
    {
      id: "handlerEmail",
      label: "Handling staff",
      ...staffColumnSizing.person,
      sortable: true,
      cell: (row) =>
        row.handlerId && row.handlerEmail ? (
          <SuspendedPerson person="Handler" suspended={row.handlerSuspended}>
            <HandlerAvatar
              id={row.handlerId}
              identity={row.handlerEmail}
              name={row.handlerName}
              pictureId={row.handlerPictureId}
            />
          </SuspendedPerson>
        ) : (
          <span className={table.muted}>Unassigned</span>
        ),
    },
    {
      id: "status",
      label: "Status",
      ...staffColumnSizing.shortStatus,
      sortable: true,
      cell: (row) => <StaffTestDriveStatus status={row.status} table />,
    },
    {
      id: "startsAt",
      label: "Booked time",
      ...staffColumnSizing.dateTime,
      sortable: true,
      cell: (row) => (
        <time className={table.date} dateTime={row.startsAt}>
          {formatMalaysianDateTime(row.startsAt)}
        </time>
      ),
    },
  ];
  if (group) {
    columns.push({
      id: "branchName",
      label: "Branch",
      ...staffColumnSizing.branch,
      sortable: true,
      cell: (row) => row.branchName,
    });
  }
  const filteredColumns = columns.map((column) =>
    !(column.id in indicators)
      ? column
      : {
          ...column,
          filter: {
            active: indicators[column.id],
            content: (
              <StaffTestDriveFilters
                list={list}
                column={column.id}
                initialBranches={branches}
                disabled={disabled || busy}
              />
            ),
          },
        },
  );
  const applied: AppliedFilter<TestDriveFilters>[] = [];
  if (filters.from || filters.to) {
    applied.push({
      key: "dates",
      label: `${filters.from} – ${filters.to}`,
      without: { ...filters, from: "", to: "" },
    });
  }
  if (filters.handlerId) {
    applied.push({
      key: "handlerId",
      label:
        snapshot.items.find((row) => row.handlerId === filters.handlerId)?.handlerName ??
        "Selected handling staff",
      without: { ...filters, handlerId: "" },
    });
  }
  if (filters.handler) {
    applied.push({
      key: "handler",
      label: `Handler: ${filters.handler}`,
      without: { ...filters, handler: "" },
    });
  }
  const textApplied = [
    ["registration", "Registration", filters.registration],
    ["vehicle", "Vehicle", filters.vehicle],
    ["customer", "Customer", filters.customer],
  ] as const;
  for (const [key, label, value] of textApplied) {
    if (value) {
      applied.push({ key, label: `${label}: ${value}`, without: { ...filters, [key]: "" } });
    }
  }
  if (filters.branchId) {
    applied.push({
      key: "branch",
      label:
        branches?.items.find((row) => row.id === filters.branchId)?.name ??
        snapshot.items.find((row) => row.branchId === filters.branchId)?.branchName ??
        "Selected branch",
      without: { ...filters, branchId: "" },
    });
  }
  const sort = filters.sort ? { column: filters.sort, direction: filters.direction } : null;
  return (
    <div className={styles.page}>
      <div className={styles.body}>
        <DataTable
          label="Scoped test drives"
          rows={snapshot.items}
          columns={filteredColumns}
          rowId={(row) => row.id}
          page={snapshot}
          sorting={{
            value: sort,
            onChange: (next) =>
              void list.load(
                { ...filters, sort: next?.column ?? "", direction: next?.direction ?? "asc" },
                1,
              ),
          }}
          selection={selection.selection}
          selectedRowId={selectedId}
          disabled={disabled}
          busy={busy}
          onRowClick={(row) => open(row.id)}
          onPage={(page, pageSize) => void list.load({ ...filters, pageSize }, page)}
          emptyState={{
            title: "No matching test drives",
            description: "Choose another section, search or filter.",
          }}
          toolbar={
            <>
              <StaffQueueTabs
                label="Test drive sections"
                countNoun="test drives"
                tabs={testDriveSections.map((item) => ({
                  ...item,
                  count: snapshot.counts?.[item.count],
                }))}
                value={filters.status}
                disabled={disabled || busy}
                onChange={(status) => void list.load({ ...filters, status }, 1)}
              />
              <RecordToolbar
                filters={null}
                disabled={disabled}
                filterCount={applied.length}
                filterSummary={
                  <AppliedWorklistFilters
                    filters={applied}
                    disabled={disabled || busy}
                    onRemove={(next) => void list.load(next, 1)}
                  />
                }
                actions={
                  <>
                    {applied.length > 0 && (
                      <Button
                        size="compact"
                        variant="ghost"
                        disabled={disabled || busy}
                        onClick={() =>
                          void list.load(
                            {
                              ...filters,
                              from: "",
                              to: "",
                              branchId: "",
                              handlerId: "",
                              handler: "",
                              registration: "",
                              vehicle: "",
                              customer: "",
                            },
                            1,
                          )
                        }
                      >
                        Reset filters
                      </Button>
                    )}
                    <CsvExportActions
                      selected={selection.selectedRows.length}
                      disabled={disabled || busy || snapshot.total === 0}
                      busy={csv.busy}
                      onClear={selection.clear}
                      onSelected={() => csv.exportSelected(selection.selectedRows)}
                      onAll={() => void csv.exportAll()}
                    />
                  </>
                }
              >
                <form
                  className={styles.search}
                  onSubmit={(event) => {
                    event.preventDefault();
                    if (!busy) {
                      void list.load({ ...filters, search: draft.search.trim() }, 1);
                    }
                  }}
                >
                  <SearchField
                    label="Search test drives"
                    placeholder="Search reference, vehicle or customer"
                    value={draft.search}
                    disabled={disabled}
                    busy={busy}
                    onChange={(search) => setDraft((current) => ({ ...current, search }))}
                    onClear={
                      filters.search && !busy
                        ? () => void list.load({ ...filters, search: "" }, 1)
                        : undefined
                    }
                  />
                </form>
              </RecordToolbar>
            </>
          }
        />
      </div>
    </div>
  );
}
