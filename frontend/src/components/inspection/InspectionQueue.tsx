"use client";

import { StaffLink } from "@/components/staff/StaffLink";
import type { DataColumn } from "@/components/ui/data-table/types";
import { useInspectionQueue } from "@/hooks/use-inspection-queue";
import { parseInspectionPage } from "@/lib/inspection/parse";
import { label, type InspectionRow, type QueueFilters } from "@/lib/inspection/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Branch, Page } from "@/lib/intake/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { QueueView } from "./QueueView";
import styles from "@/components/staff/IntakeWorklist.module.css";

export function InspectionQueue({
  actor,
  initial,
  filters,
  page,
  branches,
}: {
  actor: CurrentActor;
  initial: ApiResult<Page<InspectionRow>>;
  filters: QueueFilters;
  page: number;
  branches: Page<Branch> | null;
}) {
  const state = useInspectionQueue(
    "vehicles",
    initial,
    filters,
    page,
    branches,
    parseInspectionPage,
  );

  const columns: DataColumn<InspectionRow>[] = [
    {
      id: "registration",
      label: "Registration",
      minWidth: 150,
      cell: (row) =>
        row.inspectionId ? (
          <StaffLink prefetch={false} href={`/dashboard/inspection/${row.inspectionId}`}>
            {row.registrationPlate}
          </StaffLink>
        ) : (
          row.registrationPlate
        ),
      exportValue: (row) => row.registrationPlate,
    },
    {
      id: "vehicle",
      label: "Vehicle",
      minWidth: 160,
      cell: (r) => `${r.make} ${r.model}`,
      exportValue: (r) => `${r.make} ${r.model}`,
    },
    {
      id: "branch",
      label: "Branch",
      minWidth: 150,
      cell: (r) => r.branchName,
      exportValue: (r) => r.branchName,
    },
    {
      id: "checkedIn",
      label: "Checked in",
      minWidth: 185,
      cell: (r) => <span className={styles.timing}>{formatMalaysianDateTime(r.checkedInAt)}</span>,
      exportValue: (r) => formatMalaysianDateTime(r.checkedInAt),
    },
    {
      id: "progress",
      label: "Inspection",
      minWidth: 110,
      cell: (r) => label(r.status),
      exportValue: (r) => label(r.status),
    },
    {
      id: "saleStatus",
      label: "Vehicle status",
      minWidth: 150,
      cell: (r) => label(r.vehicleStatus),
      exportValue: (r) => label(r.vehicleStatus),
    },
  ];
  return (
    <QueueView
      actor={actor}
      kind="vehicles"
      state={state}
      columns={columns}
      rowId={(r) => r.vehicleId}
      parse={parseInspectionPage}
    />
  );
}
