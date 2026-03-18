"use client";

import { useState } from "react";
import { Dialog } from "@/components/ui/Dialog";
import { StaffLink } from "@/components/staff/StaffLink";
import { WorkshopStatus } from "./WorkshopStatus";
import type { useInspectionEditor } from "@/hooks/use-inspection-editor";
import { assessmentLabel } from "@/lib/inspection/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { formatCurrency } from "@/lib/currency-format";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import workshop from "./Workshop.module.css";
import { workshopSnapshotStatus, workshopStatusLabel } from "@/lib/inspection/workshop-contracts";

export function InspectionRecord({
  id,
  editor,
}: {
  id: string;
  editor: ReturnType<typeof useInspectionEditor>;
}) {
  const { snapshot, base } = editor;
  if (!snapshot || !base) {
    return null;
  }
  const status = workshopSnapshotStatus(snapshot);
  const request = snapshot.repairState;
  const summaryLabel = request ? "Current request budget" : "Current work";
  const summaryValue = request ? formatCurrency(request.budget) : workshopStatusLabel(status);
  const findingCount = Object.values(editor.value.entries).filter(
    (entry) => entry.kind === "Finding",
  ).length;
  return (
    <section className={workshop.summary} aria-label="Vehicle and confirmed assessment">
      <div className={workshop.summaryMain}>
        <div>
          <span className={workshop.summaryLabel}>{summaryLabel}</span>
          <strong className={workshop.summaryValue}>{summaryValue}</strong>
        </div>
        <div className={workshop.summaryContext}>
          {request && <WorkshopStatus status={status} />}
          <span>
            Revision {snapshot.draft.revision} · {snapshot.draft.isSigned ? "Signed" : "Draft"}
          </span>
          <span>
            {findingCount} {findingCount === 1 ? "finding" : "findings"}
          </span>
        </div>
      </div>
      <div className={workshop.summaryLinks}>
        {snapshot.report && (
          <StaffLink
            prefetch={false}
            target="_blank"
            rel="noopener"
            href={`/dashboard/inspection/${id}/reports/${snapshot.report.revision}`}
          >
            Open signed report · revision {snapshot.report.revision}
          </StaffLink>
        )}
        <InspectionDetails editor={editor} />
      </div>
      {editor.stale && (
        <p role="alert">
          A newer assignment, save or revision is available. Your draft remains retained. Discard it
          to load the confirmed checklist before editing again.
        </p>
      )}
      {editor.confirmed && (
        <p role="alert">
          The action was confirmed, but the latest inspection read is unavailable. Refresh before
          another action.
        </p>
      )}
      {editor.pendingSnapshot && (
        <p role="status">
          Fresh inspection data is available. Local form drafts remain retained. Finish or discard
          them to use the fresh snapshot.
        </p>
      )}
      {editor.save.uncertain && (
        <p role="alert">
          Save is unconfirmed. Reconcile the exact captured checklist and operation.
        </p>
      )}
      {editor.sign.uncertain && (
        <p role="alert">Sign-off is unconfirmed. Reconcile the same saved version and revision.</p>
      )}
    </section>
  );
}

export function InspectionDetails({ editor }: { editor: ReturnType<typeof useInspectionEditor> }) {
  const [open, setOpen] = useState(false);
  const { snapshot } = editor;
  if (!snapshot) {
    return null;
  }
  const request = snapshot.currentRequest;
  const title = request ? "Vehicle & request details" : "Vehicle & inspection details";
  return (
    <div className={workshop.metadata}>
      <button type="button" className={workshop.metadataTrigger} onClick={() => setOpen(true)}>
        {title}
      </button>
      <Dialog open={open} title={title} onClose={() => setOpen(false)}>
      <dl className={workshop.facts}>
        <div>
          <dt>VIN</dt>
          <dd className={workbench.reference}>{snapshot.draft.vehicle.vin}</dd>
        </div>
        <div>
          <dt>Mileage</dt>
          <dd>{snapshot.draft.vehicle.mileageKilometres.toLocaleString("en-MY")} km</dd>
        </div>
        <div>
          <dt>Assigned technician</dt>
          <dd>{snapshot.report?.technicianIdentity ?? snapshot.draft.technicianId}</dd>
        </div>
        <div>
          <dt>Saved assessment</dt>
          <dd>
            {snapshot.draft.latestSave
              ? assessmentLabel(snapshot.draft.latestSave.assessment)
              : "No saved assessment"}
          </dd>
        </div>
        <div>
          <dt>Saved estimate</dt>
          <dd>
            {snapshot.draft.latestSave
              ? formatCurrency(snapshot.draft.latestSave.assessment.estimatedRepairCost)
              : "No saved assessment"}
          </dd>
        </div>
        {request && (
          <>
            <div>
              <dt>Requested</dt>
              <dd>{formatMalaysianDateTime(request.requestedAt)}</dd>
            </div>
            <div>
              <dt>Work version</dt>
              <dd>
                {request.version} · Signed revision {request.revision}
              </dd>
            </div>
          </>
        )}
      </dl>
      </Dialog>
    </div>
  );
}
