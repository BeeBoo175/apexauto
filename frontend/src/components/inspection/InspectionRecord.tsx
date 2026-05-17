"use client";

import { StaffDetailsDialog, type StaffDetail } from "@/components/staff/StaffDetailsDialog";
import { StaffLink } from "@/components/staff/StaffLink";
import { WorkshopStatus } from "./WorkshopStatus";
import type { useInspectionEditor } from "@/hooks/use-inspection-editor";
import { assessmentLabel, reportDocumentHref } from "@/lib/inspection/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { formatCurrency } from "@/lib/currency-format";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import workshop from "./Workshop.module.css";
import { workshopSnapshotStatus } from "@/lib/inspection/workshop-contracts";

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
  const assessment = snapshot.report?.assessment ?? snapshot.draft.latestSave?.assessment;
  const summaryLabel = request ? "Requested repair budget" : "Saved repair estimate";
  const estimate = request?.budget ?? assessment?.estimatedRepairCost;
  const summaryValue = estimate === undefined ? "Not assessed" : formatCurrency(estimate);
  const findingCount = Object.values(editor.value.entries).filter(
    (entry) => entry.kind === "Finding",
  ).length;
  return (
    <section className={workbench.summary} aria-label="Vehicle and confirmed assessment">
      <div className={workbench.summaryMain}>
        <div>
          <span className={workbench.summaryLabel}>{summaryLabel}</span>
          <strong className={workbench.summaryValue}>{summaryValue}</strong>
        </div>
        <div className={workbench.summaryContext}>
          <WorkshopStatus status={status} />
          <span>
            {`Revision ${snapshot.draft.revision} · ${snapshot.draft.isSigned ? "Signed" : "Draft"}`}
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
            href={reportDocumentHref(id, snapshot.report.revision)}
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
  const { snapshot } = editor;
  if (!snapshot) {
    return null;
  }
  const request = snapshot.currentRequest;
  const title = request ? "Vehicle & request details" : "Vehicle & inspection details";
  const details: StaffDetail[] = [
    {
      label: "VIN",
      value: <span className={workbench.reference}>{snapshot.draft.vehicle.vin}</span>,
    },
    {
      label: "Mileage",
      value: `${snapshot.draft.vehicle.mileageKilometres.toLocaleString("en-MY")} km`,
    },
    {
      label: "Assigned technician",
      value: snapshot.report?.technicianIdentity ?? snapshot.draft.technicianIdentity,
    },
    {
      label: "Saved assessment",
      value: snapshot.draft.latestSave
        ? assessmentLabel(snapshot.draft.latestSave.assessment)
        : "No saved assessment",
    },
    {
      label: "Saved estimate",
      value: snapshot.draft.latestSave
        ? formatCurrency(snapshot.draft.latestSave.assessment.estimatedRepairCost)
        : "No saved assessment",
    },
  ];
  if (request) {
    details.push(
      { label: "Requested", value: formatMalaysianDateTime(request.requestedAt) },
      { label: "Work version", value: `${request.version} · Signed revision ${request.revision}` },
    );
  }
  return <StaffDetailsDialog title={title} details={details} />;
}
