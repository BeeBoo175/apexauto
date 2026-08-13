"use client";
import { ResolvedPersonLabel } from "@/components/staff/ResolvedPersonLabel";
import { useState } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Input";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Dialog } from "@/components/ui/Dialog";
import { useInspectionManager } from "@/hooks/use-inspection-manager";
import { useEditGuard } from "@/hooks/use-edit-guard";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Draft, Technician } from "@/lib/inspection/contracts";
import type { Page } from "@/lib/api";
import { AssignmentForm } from "./AssignmentForm";
import styles from "./Inspection.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function ManagerActions({
  actor,
  draft,
  technicians,
  disabled,
  onLock,
  onConfirmed,
  onDraft,
  actionsContainer,
  active = true,
}: {
  actor: CurrentActor;
  draft: Draft;
  technicians: Page<Technician> | null;
  disabled: boolean;
  onLock: (locked: boolean) => void;
  onConfirmed: () => Promise<void>;
  onDraft: (dirty: boolean) => void;
  // The workspace footer holds the primary actions, so the reopen and reassign control lives there.
  actionsContainer?: HTMLElement | null;
  active?: boolean;
}) {
  const [confirming, setConfirming] = useState(false);
  const [open, setOpen] = useState(false);
  const manager = useInspectionManager({
    actor,
    draft,
    onLock,
    onConfirmed,
    onDraft,
    onClose: () => setOpen(false),
  });
  const { reason, command } = manager;

  if (actor.role !== "BranchManager" || !active) {
    return null;
  }
  const trigger = (
    <Button size="compact" variant="secondary" disabled={disabled} onClick={() => setOpen(true)}>
      {draft.isSigned ? "Reopen inspection" : "Reassign technician"}
    </Button>
  );
  return (
    <section className={`${styles.section} ${workbench.protocol}`}>
      <div className={workbench.toolbar}>
        <h2 className={workbench.sectionTitle}>Assignment & revision</h2>
        {!actionsContainer && trigger}
      </div>
      {actionsContainer && createPortal(trigger, actionsContainer)}
      <p className={styles.help}>
        Assigned technician:{" "}
        <ResolvedPersonLabel id={draft.technicianId} identity={draft.technicianIdentity} />.
        Assignment version {draft.assignmentVersion}.
      </p>
      <Dialog
        open={open}
        title={draft.isSigned ? "Reopen signed inspection" : "Reassign unsigned inspection"}
        closeDisabled={command.busy || command.uncertain}
        onClose={() => setOpen(false)}
      >
        {!draft.isSigned && (
          <AssignmentForm
            actor={actor}
            vehicleId={draft.vehicleId}
            version={draft.assignmentVersion}
            initialChoices={technicians}
            onLocked={manager.lock}
            onDraft={onDraft}
            onConfirmed={manager.assignmentConfirmed}
          />
        )}
        {draft.isSigned && (
          <ReopeningDraft manager={manager}>
            <form
              className={styles.form}
              onSubmit={async (e) => {
                e.preventDefault();
                if (command.uncertain) {
                  await manager.reopen();
                  return;
                }
                setConfirming(true);
              }}
            >
              <p>
                Reopening removes listing availability and starts a new revision. The signed report
                and repair history remain retained.
              </p>
              <Textarea
                label="Reopening reason"
                placeholder="Explain what needs reviewing in the signed inspection"
                required
                maxLength={1000}
                rows={3}
                value={reason}
                disabled={command.busy || command.uncertain}
                onChange={(e) => manager.changeReason(e.target.value)}
              />
              {command.uncertain && (
                <p role="alert">
                  Reopening is unconfirmed. Reconcile the same reason, revision and operation before
                  leaving.
                </p>
              )}
              {reason && (
                <Button
                  size="compact"
                  variant="secondary"
                  disabled={command.busy || command.uncertain}
                  onClick={() => manager.changeReason("")}
                >
                  Discard reason
                </Button>
              )}
              <Button size="compact" type="submit" disabled={command.busy}>
                {command.uncertain ? "Reconcile reopening" : "Confirm reopening"}
              </Button>
            </form>
          </ReopeningDraft>
        )}
      </Dialog>
      <ConfirmationDialog
        open={confirming}
        title="Reopen this signed inspection?"
        confirmLabel="Reopen inspection"
        danger
        busy={command.busy}
        onClose={() => setConfirming(false)}
        onConfirm={async () => {
          await manager.reopen();
          setConfirming(false);
        }}
      >
        <p>
          This removes listing availability and starts a new inspection revision. The signed report
          and repair history remain retained.
        </p>
      </ConfirmationDialog>
    </section>
  );
}

function ReopeningDraft({
  manager,
  children,
}: {
  manager: ReturnType<typeof useInspectionManager>;
  children: React.ReactNode;
}) {
  useEditGuard({
    dirty: Boolean(manager.reason),
    pending: manager.command.busy,
    uncertain: manager.command.uncertain,
    label: "Inspection reopening reason",
    discard: () => manager.changeReason(""),
  });
  return children;
}
