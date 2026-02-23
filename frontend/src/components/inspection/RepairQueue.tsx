"use client";
import { StaffLink } from "@/components/staff/StaffLink";
import { Button } from "@/components/ui/Button";
import type { DataColumn } from "@/components/ui/data-table/types";
import { useInspectionQueue } from "@/hooks/use-inspection-queue";
import { parseRepairPage } from "@/lib/inspection/parse";
import { label, type RepairRow, type QueueFilters } from "@/lib/inspection/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Branch, Page } from "@/lib/intake/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { formatCurrency } from "@/lib/currency-format";
import { QueueView } from "./QueueView";
import { InspectionDrawer } from "./InspectionDrawer";
import { useInspectionDrawer, type RepairSelection } from "@/hooks/use-inspection-drawer";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import styles from "@/components/staff/IntakeWorklist.module.css";

export function RepairQueue({
  actor,
  initial,
  filters,
  page,
  branches,
  selectedId = "",
  selectedInspection = null,
  selectedRequest = null,
}: {
  actor: CurrentActor;
  initial: ApiResult<Page<RepairRow>>;
  filters: QueueFilters;
  page: number;
  branches: Page<Branch> | null;
  selectedId?: string;
  selectedInspection?: ApiResult<EditorSnapshot> | null;
  selectedRequest?: RepairSelection | null;
}) {
  const state = useInspectionQueue(
    "repair-requests",
    initial,
    filters,
    page,
    branches,
    parseRepairPage,
  );
  const selection = useInspectionDrawer(
    selectedId,
    selectedInspection,
    () => void state.load(),
    selectedRequest,
  );
  function open(row: RepairRow) {
    selection.open(row.inspectionId, { requestId: row.requestId, revision: row.revision });
  }
  const columns: DataColumn<RepairRow>[] = [
    {
      id: "registration",
      label: "Registration",
      minWidth: 150,
      cell: (r) => (
        <Button
          className={styles.registration}
          size="row"
          variant="ghost"
          aria-label={`Open ${r.registrationPlate}`}
          aria-haspopup="dialog"
          onClick={() => open(r)}
        >
          {r.registrationPlate}
        </Button>
      ),
      exportValue: (r) => r.registrationPlate,
    },
    {
      id: "branch",
      label: "Branch",
      minWidth: 150,
      cell: (r) => r.branchName,
      exportValue: (r) => r.branchName,
    },
    {
      id: "requested",
      label: "Requested",
      minWidth: 185,
      cell: (r) => <span className={styles.timing}>{formatMalaysianDateTime(r.requestedAt)}</span>,
      exportValue: (r) => formatMalaysianDateTime(r.requestedAt),
    },
    {
      id: "version",
      label: "Revision / work",
      minWidth: 125,
      cell: (r) => `${r.revision} / ${r.repairVersion}`,
      exportValue: (r) => `${r.revision} / ${r.repairVersion}`,
    },
    {
      id: "budget",
      label: "Budget",
      align: "right",
      minWidth: 145,
      cell: (r) => formatCurrency(r.budget),
      exportValue: (r) => r.budget.toFixed(2),
    },
    {
      id: "status",
      label: "Repair status",
      minWidth: 170,
      cell: (r) => label(r.status),
      exportValue: (r) => label(r.status),
    },
    {
      id: "report",
      label: "Report",
      minWidth: 130,
      cell: (r) => (
        <StaffLink
          prefetch={false}
          target="_blank"
          rel="noopener"
          href={`/dashboard/inspection/${r.inspectionId}/reports/${r.revision}`}
        >
          View revision {r.revision}
        </StaffLink>
      ),
    },
  ];
  return (
    <QueueView
      actor={actor}
      kind="repair-requests"
      state={state}
      columns={columns}
      rowId={(r) => r.requestId}
      parse={parseRepairPage}
      onOpen={open}
      selectedRowId={selection.request?.requestId}
      disabled={Boolean(selection.id)}
    >
      <InspectionDrawer actor={actor} selection={selection} repairs />
    </QueueView>
  );
}
