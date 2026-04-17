"use client";

import { useRef, useState } from "react";
import { Button } from "@/components/ui/Button";
import type { CurrentActor } from "@/lib/auth/actor";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import { canRequestRepair } from "@/lib/inspection/repair-availability";
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
  editing,
  onEditingChange,
}: {
  actor: CurrentActor;
  snapshot: EditorSnapshot;
  disabled: boolean;
  onLock: (v: boolean) => void;
  onConfirmed: () => Promise<void>;
  onDraft: (name: string, dirty: boolean) => void;
  active?: boolean;
  actionsContainer?: HTMLElement | null;
  editing: boolean;
  onEditingChange: (editing: boolean) => void;
}) {
  const [completionDirty, setCompletionDirty] = useState(false);
  const heading = useRef<HTMLHeadingElement>(null);
  const { draft, report, currentRequest: latest, repairState } = snapshot;
  if (!report || !draft.isSigned) {
    return active ? (
      <p className={workbench.help}>Sign the inspection before requesting repair work.</p>
    ) : null;
  }
  const canRequest = canRequestRepair(snapshot, actor);

  function changeMode(next: boolean) {
    onEditingChange(next);
    heading.current?.focus();
  }

  return (
    <>
      {active && (
        <section className={`${styles.section} ${workbench.stack}`}>
          <div className={`${workbench.sectionHeader} ${styles.repairHeading}`}>
            <h2 ref={heading} tabIndex={-1} className={workbench.sectionTitle}>
              {editing ? "Edit repair work & budget" : "Current repair work"}
            </h2>
            {canRequest && !editing && (
              <Button
                size="compact"
                variant="secondary"
                disabled={disabled || completionDirty}
                onClick={() => changeMode(true)}
              >
                {latest ? "Revise work & budget" : "Request repair work"}
              </Button>
            )}
          </div>
          {completionDirty && !editing && (
            <p className={workbench.help}>
              Complete this request or clear the completion notes before revising its work.
            </p>
          )}
          {!editing && !latest && !report.assessment.isRejected && (
            <p className={workbench.help}>
              No repair work has been requested for this signed revision.
            </p>
          )}
          {!editing && latest && (
            <>
              <RepairRequestDetails request={latest} report={report} />
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
          active={active && !editing}
          onLock={onLock}
          onConfirmed={onConfirmed}
          onDraft={(dirty) => {
            setCompletionDirty(dirty);
            onDraft("completion", dirty);
          }}
          actionsContainer={actionsContainer}
        />
      )}
      {canRequest && (
        <RepairRequestForm
          key={`${draft.revision}:${draft.repairVersion}`}
          actor={actor}
          draft={draft}
          report={report}
          latest={latest}
          disabled={disabled}
          active={active && editing}
          onLock={onLock}
          onConfirmed={async () => {
            changeMode(false);
            await onConfirmed();
          }}
          onCancel={() => changeMode(false)}
          onDraft={(dirty) => onDraft("request", dirty)}
          actionsContainer={actionsContainer}
        />
      )}
    </>
  );
}
