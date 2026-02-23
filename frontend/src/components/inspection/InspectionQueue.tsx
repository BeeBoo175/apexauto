"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import type { DataColumn } from "@/components/ui/data-table/types";
import { useInspectionQueue } from "@/hooks/use-inspection-queue";
import { useInspectionLock } from "@/hooks/use-inspection-lock";
import { parseInspectionPage } from "@/lib/inspection/parse";
import { label, type InspectionRow, type QueueFilters } from "@/lib/inspection/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Branch, Page } from "@/lib/intake/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { QueueView } from "./QueueView";
import { AssignmentForm } from "./AssignmentForm";
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
  const router = useRouter();
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
  function open(row: InspectionRow) {
    if (row.inspectionId) {
      router.push(`/dashboard/inspection/${row.inspectionId}`);
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
      rowId={(r) => r.vehicleId}
      parse={parseInspectionPage}
      onOpen={open}

      disabled={Boolean(selected)}
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
              router.push(`/dashboard/inspection/${id}`);
            }}
          />
        )}
      </Dialog>
    </QueueView>
  );
}
