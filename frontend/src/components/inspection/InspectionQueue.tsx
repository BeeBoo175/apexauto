"use client";
import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import type { DataColumn } from "@/components/ui/data-table/types";
import { useInspectionQueue } from "@/hooks/use-inspection-queue";
import { useInspectionLock } from "@/hooks/use-inspection-lock";
import { parseInspectionPage } from "@/lib/inspection/parse";
import { inspectionExportColumns } from "@/lib/inspection/export-columns";
import { label, type InspectionRow, type QueueFilters } from "@/lib/inspection/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Page } from "@/lib/api";
import type { Branch } from "@/lib/branches";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { QueueView } from "./QueueView";
import { AssignmentForm } from "./AssignmentForm";
import { InspectionDrawer } from "./InspectionDrawer";
import { useInspectionDrawer } from "@/hooks/use-inspection-drawer";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import styles from "@/components/staff/IntakeWorklist.module.css";

export function InspectionQueue({
  actor,
  initial,
  filters,
  page,
  branches,
  selectedId = "",
  selectedInspection = null,
}: {
  actor: CurrentActor;
  initial: ApiResult<Page<InspectionRow>>;
  filters: QueueFilters;
  page: number;
  branches: Page<Branch> | null;
  selectedId?: string;
  selectedInspection?: ApiResult<EditorSnapshot> | null;
}) {
  const state = useInspectionQueue(
    "vehicles",
    initial,
    filters,
    page,
    branches,
    parseInspectionPage,
  );
  const [selected, setSelected] = useState<InspectionRow | null>(null);
  const [locked, setLocked] = useState(false);
  const lockNavigation = useInspectionLock(locked);
  const selection = useInspectionDrawer(selectedId, selectedInspection, () => void state.load());
  function open(row: InspectionRow) {
    if (row.inspectionId) {
      selection.open(row.inspectionId);
      return;
    }
    setSelected(row);
  }
  const columns: DataColumn<InspectionRow>[] = [
    {
      id: "registration",
      label: "Registration",
      minWidth: 150,
      cell: (row) => (
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
      ),
    },
    {
      id: "vehicle",
      label: "Vehicle",
      minWidth: 160,
      cell: (r) => `${r.make} ${r.model}`,
    },
    {
      id: "branch",
      label: "Branch",
      minWidth: 150,
      cell: (r) => r.branchName,
    },
    {
      id: "checkedIn",
      label: "Checked in",
      minWidth: 185,
      cell: (r) => <span className={styles.timing}>{formatMalaysianDateTime(r.checkedInAt)}</span>,
    },
    {
      id: "progress",
      label: "Inspection",
      minWidth: 110,
      cell: (r) => label(r.status),
    },
    {
      id: "saleStatus",
      label: "Vehicle status",
      minWidth: 150,
      cell: (r) => label(r.vehicleStatus),
    },
    {
      id: "action",
      label: "Action",
      minWidth: 125,
      cell: (r) =>
        !r.inspectionId && actor.role === "WorkshopTechnician" ? (
          <Button size="compact" variant="secondary" onClick={() => setSelected(r)}>
            Claim inspection
          </Button>
        ) : (
          <span>{r.revision ? `Revision ${r.revision}` : "Unassigned"}</span>
        ),
    },
  ];
  return (
    <QueueView
      actor={actor}
      kind="vehicles"
      state={state}
      columns={columns}
      exportColumns={inspectionExportColumns}
      rowId={(r) => r.vehicleId}
      parse={parseInspectionPage}
      onOpen={open}
      selectedRowId={
        state.snapshot?.items.find((row) => row.inspectionId === selection.id)?.vehicleId
      }
      disabled={Boolean(selected) || Boolean(selection.id)}
    >
      <Dialog
        open={Boolean(selected)}
        title={`${actor.role === "WorkshopTechnician" ? "Claim inspection" : "Unassigned inspection"} · ${selected?.registrationPlate ?? ""}`}
        closeDisabled={locked}
        onClose={() => setSelected(null)}
      >
        {selected && actor.role !== "WorkshopTechnician" && (
          <p>This vehicle is awaiting a workshop technician&apos;s first claim.</p>
        )}
        {selected && actor.role === "WorkshopTechnician" && (
          <AssignmentForm
            key={selected.vehicleId}
            actor={actor}
            vehicleId={selected.vehicleId}
            version={0}
            initialChoices={null}
            onLocked={setLocked}
            onConfirmed={async (id) => {
              lockNavigation(false);
              setLocked(false);
              setSelected(null);
              await state.load();
              selection.open(id);
            }}
          />
        )}
      </Dialog>
      <InspectionDrawer actor={actor} selection={selection} />
    </QueueView>
  );
}
