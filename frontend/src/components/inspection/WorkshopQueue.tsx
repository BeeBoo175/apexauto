"use client";

import { useState } from "react";
import { HandlerAvatar } from "@/components/staff/HandlerAvatar";
import { VehicleDescription, VehicleRegistration } from "@/components/staff/VehicleTableCells";
import { Dialog } from "@/components/ui/Dialog";
import type { DataColumn } from "@/components/ui/data-table/types";
import { useWorkshopManager } from "@/hooks/use-workshop-manager";
import type { RepairSelection } from "@/hooks/use-inspection-drawer";
import { parseWorkshopPage } from "@/lib/inspection/parse-workshop";
import { workshopExportColumns } from "@/lib/inspection/workshop-export";
import {
  workshopDate,
  workshopRowSection,
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
import { staffColumnSizing } from "@/lib/staff/table-columns";
import { QueueView } from "./QueueView";
import { InspectionDrawer } from "./InspectionDrawer";
import { WorkshopStatus } from "./WorkshopStatus";
import styles from "@/components/staff/StaffTable.module.css";

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
  selectedCategory = "",
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
  selectedCategory?: string;
  selectedReview?: SelectedRepairReview | null;
}) {
  const {
    queue,
    drawer: selection,
    refresh,
  } = useWorkshopManager({
    initial,
    filters,
    page,
    branches,
    selectedId,
    selectedInspection,
    selectedRequest,
    selectedSection,
    selectedCategory,
  });
  const state = { ...queue, refresh };
  const [unassigned, setUnassigned] = useState<WorkshopRow | null>(null);

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
      label: "Registration",
      sortable: true,
      ...staffColumnSizing.registration,
      cell: (row) => <VehicleRegistration registration={row.registrationPlate} />,
    },
    {
      id: "vehicle",
      label: "Vehicle",
      sortable: true,
      ...staffColumnSizing.vehicle,
      cell: (row) => <VehicleDescription vehicle={row} />,
    },
    {
      id: "status",
      label: "Current work",
      sortable: true,
      ...staffColumnSizing.status,
      cell: (row) => <WorkshopStatus status={row.status} table />,
    },
  ];
  if (actor.role !== "WorkshopTechnician") {
    columns.push({
      id: "technicianIdentity",
      label: "Assigned technician",
      sortable: true,
      ...staffColumnSizing.person,
      cell: (row) =>
        row.technicianId && row.technicianIdentity ? (
          <HandlerAvatar
            id={row.technicianId}
            identity={row.technicianIdentity}
            name={row.technicianIdentity}
            pictureId={row.technicianPictureId}
          />
        ) : (
          <span className={styles.muted}>Unassigned</span>
        ),
    });
  }
  columns.push({
    id: "budget",
    label: "Requested budget",
    sortable: true,
    ...staffColumnSizing.money,
    align: "right",
    cell: (row) => (
      <span className={row.budget === null ? styles.muted : styles.money}>
        {row.budget === null ? "No request" : formatCurrency(row.budget)}
      </span>
    ),
  });
  const date = workshopDate(state.active.filters.status);
  columns.push({
    id: date.column,
    label: date.label,
    sortable: true,
    ...staffColumnSizing.dateTime,
    cell: (row) => {
      const at = row[date.column];
      if (!at) {
        return <span className={styles.muted}>No request</span>;
      }
      return (
        <time className={styles.date} dateTime={at}>
          {formatMalaysianDateTime(at)}
        </time>
      );
    },
  });
  if (actor.role === "GeneralManager") {
    columns.push({
      id: "branchName",
      label: "Branch",
      sortable: true,
      ...staffColumnSizing.branch,
      cell: (row) => row.branchName,
    });
  }

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
        unassigned?.vehicleId ??
        state.snapshot?.items.find(
          (row) => row.inspectionId === (selection.pendingId || selection.id),
        )?.vehicleId
      }
      disabled={Boolean(unassigned) || Boolean(selection.id) || selection.busy}
    >
      <Dialog
        open={Boolean(unassigned)}
        title={`Unassigned inspection Â· ${unassigned?.registrationPlate ?? ""}`}
        onClose={() => setUnassigned(null)}
      >
        {unassigned && <p>This vehicle is awaiting a workshop technician&apos;s first claim.</p>}
      </Dialog>
      <InspectionDrawer actor={actor} selection={selection} selectedReview={selectedReview} />
    </QueueView>
  );
}
