"use client";
import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Input";
import { Dialog } from "@/components/ui/Dialog";
import { useInspectionManager } from "@/hooks/use-inspection-manager";
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
  active = true,
}: {
  actor: CurrentActor;
  draft: Draft;
  technicians: Page<Technician> | null;
  disabled: boolean;
  onLock: (locked: boolean) => void;
  onConfirmed: () => Promise<void>;
  onDraft: (dirty: boolean) => void;
  active?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const manager = useInspectionManager({
    actor,
    draft,
    onLock,
    onConfirmed,
    onDraft,
    onClose: () => setOpen(false),
  });
  const { reason, locked, command } = manager;

  if (actor.role !== "BranchManager" || !active) {
    return null;
  }
  return (
    <section className={`${styles.section} ${workbench.protocol}`}>
      <div className={workbench.toolbar}>
        <h2 className={workbench.sectionTitle}>Assignment & revision</h2>
        <Button
          size="compact"
          variant="secondary"
          disabled={disabled}
          onClick={() => setOpen(true)}
        >
          {draft.isSigned ? "Reopen inspection" : "Reassign technician"}
        </Button>
      </div>
      <p className={styles.help}>
        Assigned technician: {draft.technicianId}. Assignment version {draft.assignmentVersion}.
      </p>
      <Dialog
        open={open}
        title={draft.isSigned ? "Reopen signed inspection" : "Reassign unsigned inspection"}
        closeDisabled={locked || command.busy || command.uncertain}
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
          <form
            className={styles.form}
            onSubmit={async (e) => {
              e.preventDefault();
              await manager.reopen();
            }}
          >
            <p>
              Reopening removes listing availability and starts a new revision. The signed report
              and repair history remain retained.
            </p>
            <Textarea
              label="Reopening reason"
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
            <Button size="compact" type="submit" disabled={command.busy}>
              {command.uncertain ? "Reconcile reopening" : "Confirm reopening"}
            </Button>
          </form>
        )}
      </Dialog>
    </section>
  );
}
