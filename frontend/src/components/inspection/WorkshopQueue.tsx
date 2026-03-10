"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { Dialog } from "@/components/ui/Dialog";
import type { DataColumn } from "@/components/ui/data-table/types";
import { useInspectionQueue } from "@/hooks/use-inspection-queue";
import { useInspectionDrawer, type RepairSelection } from "@/hooks/use-inspection-drawer";
import { parseWorkshopPage } from "@/lib/inspection/parse-workshop";
import { workshopExportColumns } from "@/lib/inspection/workshop-export";
import {
  workshopRowSection,
  workshopStatusLabel,
  type WorkshopRow,
} from "@/lib/inspection/workshop-contracts";
import type { QueueFilters } from "@/lib/inspection/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import type { WorkshopSection } from "@/lib/inspection/query";
import type { SelectedRepairReview } from "@/lib/inspection/repair-review";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { formatCurrency } from "@/lib/currency-format";
import { QueueView } from "./QueueView";
import { InspectionDrawer } from "./InspectionDrawer";
import styles from "@/components/staff/IntakeWorklist.module.css";
import tones from "@/components/ui/StatusTone.module.css";
import workshop from "./Workshop.module.css";

const statusTones: Record<WorkshopRow["status"], string> = {
  AwaitingInspection: tones.neutral,
  InspectionUnderway: tones.info,
  RepairRequestRequired: tones.info,
  ApprovalRequired: tones.info,
  RepairsToComplete: tones.info,
  ReadyForSale: tones.success,
  Rejected: tones.neutral,
};

export function WorkshopQueue({
  actor,
  initial,
  filters,
  page,
  branches,
  selectedId = "",
  selectedInspection = null,
  selectedRequest = null,
  selectedSection = "inspection",
  selectedReview = null,
}: {
  actor: CurrentActor;
  initial: ApiResult<Page<WorkshopRow>>;
  filters: QueueFilters;
  page: number;
  branches: Page<Branch> | null;
  selectedId?: string;
  selectedInspection?: ApiResult<EditorSnapshot> | null;
  selectedRequest?: RepairSelection | null;
  selectedSection?: WorkshopSection;
  selectedReview?: SelectedRepairReview | null;
}) {
  const state = useInspectionQueue("workshop", initial, filters, page, branches, parseWorkshopPage);
  const [unassigned, setUnassigned] = useState<WorkshopRow | null>(null);
  const selection = useInspectionDrawer(
    selectedId,
    selectedInspection,
    () => void state.refresh(),
    selectedRequest,
    selectedSection,
  );

  function open(row: WorkshopRow) {
    if (!row.inspectionId) {
      setUnassigned(row);
      return;
    }
    selection.open(row.inspectionId, null, workshopRowSection(row));
  }

  const columns: DataColumn<WorkshopRow>[] = [
    {
      id: "registrationPlate",
      label: "Vehicle",
      sortable: true,
      minWidth: 210,
      cell: (row) => (
        <div className={workshop.vehicle}>
          <Button
            className={styles.registration}
            size="row"
            variant="ghost"
            aria-label={`Open ${row.registrationPlate}`}
            aria-haspopup="dialog"
            onClick={() => open(row)}
          >
            {row.registrationPlate}
          </Button>
          <span>
            {row.make} {row.model}
          </span>
        </div>
      ),
    },
    {
      id: "status",
      label: "Current work",
      sortable: true,
      minWidth: 190,
      cell: (row) => (
        <Badge className={`${tones.badge} ${statusTones[row.status]}`}>
          {workshopStatusLabel(row.status)}
        </Badge>
      ),
    },
  ];
  if (actor.role !== "WorkshopTechnician") {
    columns.push({
      id: "technicianIdentity",
      label: "Assigned technician",
      sortable: true,
      minWidth: 200,
      cell: (row) => row.technicianIdentity ?? "Unassigned",
    });
  }
  if (actor.role === "GeneralManager" && !state.active.filters.branchId) {
    columns.push({
      id: "branchName",
      label: "Branch",
      sortable: true,
      minWidth: 150,
      cell: (row) => row.branchName,
    });
  }
  if (["Approval", "Repairs"].includes(state.active.filters.status)) {
    columns.push({
      id: "budget",
      label: "Requested budget",
      sortable: true,
      minWidth: 150,
      align: "right",
      cell: (row) => (row.budget === null ? "No current request" : formatCurrency(row.budget)),
    });
  }
  columns.push({
    id: "since",
    label: "Since",
    sortable: true,
    minWidth: 190,
    cell: (row) => (
      <div className={workshop.since}>
        <span className={styles.timing}>{formatMalaysianDateTime(row.since)}</span>
        <span>{row.dateSource === "Requested" ? "Repair requested" : "Vehicle checked in"}</span>
      </div>
    ),
  });

  return (
    <QueueView
      actor={actor}
      state={state}
      columns={columns}
      exportColumns={workshopExportColumns}
      rowId={(row) => row.vehicleId}
      parse={parseWorkshopPage}
      onOpen={open}
      selectedRowId={
        state.snapshot?.items.find((row) => row.inspectionId === selection.id)?.vehicleId
      }
      disabled={Boolean(unassigned) || Boolean(selection.id) || selection.busy}
    >
      <Dialog
        open={Boolean(unassigned)}
        title={`Unassigned inspection · ${unassigned?.registrationPlate ?? ""}`}
        onClose={() => setUnassigned(null)}
      >
        {unassigned && <p>This vehicle is awaiting a workshop technician&apos;s first claim.</p>}
      </Dialog>
      <InspectionDrawer actor={actor} selection={selection} selectedReview={selectedReview} />
    </QueueView>
  );
}
