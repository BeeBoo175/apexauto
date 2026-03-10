import type { CurrentActor } from "@/lib/auth/actor";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import { RepairRequestDetails } from "./RepairRequestDetails";
import { RepairRequestForm } from "./RepairRequestForm";
import { RepairActions } from "./RepairActions";
import styles from "./Inspection.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function RepairPanel({
  actor,
  snapshot,
  disabled,
  onLock,
  onConfirmed,
  onDraft,
  active = true,
  actionsContainer,
}: {
  actor: CurrentActor;
  snapshot: EditorSnapshot;
  disabled: boolean;
  onLock: (v: boolean) => void;
  onConfirmed: () => Promise<void>;
  onDraft: (name: string, dirty: boolean) => void;
  active?: boolean;
  actionsContainer?: HTMLElement | null;
}) {
  const { draft, report, currentRequest: latest, repairState } = snapshot;
  if (!report || !draft.isSigned) {
    return active ? (
      <p className={workbench.help}>Sign the inspection before requesting repair work.</p>
    ) : null;
  }
  const canRequest =
    actor.role === "WorkshopTechnician" &&
    draft.technicianId === actor.userId &&
    !report.assessment.isRejected;
  return (
    <>
      {active && (
        <section className={`${styles.section} ${workbench.stack}`}>
          {!latest && !report.assessment.isRejected && (
            <p className={workbench.help}>
              No repair work has been requested for this signed revision.
            </p>
          )}
          {latest && (
            <>
              <RepairRequestDetails request={latest} report={report} showMetadata={false} />
              {repairState?.coveringApprovalId && (
                <p className={styles.help}>
                  {repairState.approvalId === repairState.coveringApprovalId
                    ? "Budget approved for this request."
                    : "Earlier approval covers the same work within its approved budget."}
                </p>
              )}
            </>
          )}
          {report.assessment.isRejected && (
            <p>
              Rejected vehicles cannot proceed to listing or repair completion. A branch manager can
              reopen the inspection for a new revision.
            </p>
          )}
        </section>
      )}
      {latest && repairState && (
        <RepairActions
          key={`${repairState.requestId}:${repairState.status}`}
          actor={actor}
          draft={draft}
          state={repairState}
          disabled={disabled}
          active={active}
          onLock={onLock}
          onConfirmed={onConfirmed}
          onDraft={(dirty) => onDraft("completion", dirty)}
          actionsContainer={actionsContainer}
        />
      )}
      {canRequest && report.checklist.some((p) => p.kind === "Finding") && (
        <RepairRequestForm
          key={`${draft.revision}:${draft.repairVersion}`}
          actor={actor}
          draft={draft}
          report={report}
          latest={latest}
          disabled={disabled}
          active={active}
          onLock={onLock}
          onConfirmed={onConfirmed}
          onDraft={(dirty) => onDraft("request", dirty)}
          actionsContainer={actionsContainer}
        />
      )}
    </>
  );
}
