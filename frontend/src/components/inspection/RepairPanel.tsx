import type { CurrentActor } from "@/lib/auth/actor";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import { label } from "@/lib/inspection/contracts";
import { RepairRequestDetails } from "./RepairRequestDetails";
import { RepairRequestForm } from "./RepairRequestForm";
import { RepairActions } from "./RepairActions";
import { RepairHistoryView } from "./InspectionHistory";
import styles from "./Inspection.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import { Badge } from "@/components/ui/Badge";
import tones from "@/components/ui/StatusTone.module.css";

export function RepairPanel({
  actor,
  snapshot,
  disabled,
  onLock,
  onConfirmed,
  onDraft,
}: {
  actor: CurrentActor;
  snapshot: EditorSnapshot;
  disabled: boolean;
  onLock: (v: boolean) => void;
  onConfirmed: () => Promise<void>;
  onDraft: (name: string, dirty: boolean) => void;
}) {
  const { draft, report, repairs, repairState } = snapshot;
  const latest =
    repairs?.items.find((item) => item.request.version === draft.repairVersion)?.request ?? null;
  if (!report || !draft.isSigned) {
    return null;
  }
  const canRequest =
    actor.role === "WorkshopTechnician" &&
    draft.technicianId === actor.userId &&
    !report.assessment.isRejected;
  return (
    <div id="repairs" className={styles.sections}>
      <section className={`${styles.section} ${workbench.stack}`}>
        <div className={workbench.toolbar}>
          <h2 className={workbench.sectionTitle}>Current repair work</h2>
          <Badge className={`${tones.badge} ${tones.neutral}`}>
            {repairState ? label(repairState.status) : "No repair request"}
          </Badge>
        </div>
        {!latest && !report.assessment.isRejected && (
          <p className={workbench.help}>
            No repair work has been requested for this signed revision.
          </p>
        )}
        {latest && (
          <>
            <RepairRequestDetails request={latest} report={report} />
            {repairState?.coveringApprovalId && (
              <p className={styles.help}>
                Covering approval: {repairState.coveringApprovalId}
                {repairState.approvalId === repairState.coveringApprovalId
                  ? " · exact request"
                  : " · retained authority from identical work with sufficient budget"}
              </p>
            )}
            {repairState && (
              <RepairActions
                key={`${repairState.requestId}:${repairState.status}`}
                actor={actor}
                draft={draft}
                state={repairState}
                disabled={disabled}
                onLock={onLock}
                onConfirmed={onConfirmed}
                onDraft={(dirty) => onDraft("completion", dirty)}
              />
            )}
          </>
        )}
        {report.assessment.isRejected && (
          <p>
            Rejected vehicles cannot proceed to listing or repair completion. A branch manager can
            reopen the inspection for a new revision.
          </p>
        )}
        {canRequest && report.checklist.some((p) => p.kind === "Finding") && (
          <RepairRequestForm
            key={`${draft.revision}:${draft.repairVersion}`}
            actor={actor}
            draft={draft}
            report={report}
            latest={latest}
            disabled={disabled}
            onLock={onLock}
            onConfirmed={onConfirmed}
            onDraft={(dirty) => onDraft("request", dirty)}
          />
        )}
      </section>
      {repairs && <RepairHistoryView initial={repairs} disabled={disabled} />}
    </div>
  );
}
