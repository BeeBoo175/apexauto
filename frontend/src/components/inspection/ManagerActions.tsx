"use client";
import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Input";
import { Dialog } from "@/components/ui/Dialog";
import { useApiCommand } from "@/hooks/use-api-command";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseReopening } from "@/lib/inspection/parse-report";
import { isRecord } from "@/lib/auth/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Draft, Technician } from "@/lib/inspection/contracts";
import type { Page } from "@/lib/api";
import { AssignmentForm } from "./AssignmentForm";
import styles from "./Inspection.module.css";
import workbench from "@/components/staff/AppointmentWorkbench.module.css";

export function ManagerActions({
  actor,
  draft,
  technicians,
  disabled,
  onLock,
  onConfirmed,
  onDraft,
}: {
  actor: CurrentActor;
  draft: Draft;
  technicians: Page<Technician> | null;
  disabled: boolean;
  onLock: (locked: boolean) => void;
  onConfirmed: () => Promise<void>;
  onDraft: (dirty: boolean) => void;
}) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [locked, setLocked] = useState(false);
  const command = useApiCommand(
    `drafts/${draft.id}/reopen`,
    parseReopening,
    (r, body) =>
      isRecord(body) &&
      r.operationId === body.operationId &&
      r.inspectionId === draft.id &&
      r.actorId === actor.userId &&
      r.assignmentVersion === body.assignmentVersion &&
      r.expectedRevision === body.revision,
    inspectionFetch,
  );
  function lock(value: boolean) {
    setLocked(value);
    onLock(value);
  }
  if (actor.role !== "BranchManager") {
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
            onLocked={lock}
            onDraft={onDraft}
            onConfirmed={() => {
              onDraft(false);
              setOpen(false);
              lock(false);
              void onConfirmed();
            }}
          />
        )}
        {draft.isSigned && (
          <form
            className={styles.form}
            onSubmit={async (e) => {
              e.preventDefault();
              lock(true);
              const result = await command.submit({
                operationId: crypto.randomUUID(),
                assignmentVersion: draft.assignmentVersion,
                revision: draft.revision,
                reason: reason.trim(),
              });
              if (result) {
                lock(false);
              }
              if (result?.status === 200 && result.data) {
                onDraft(false);
                setOpen(false);
                await onConfirmed();
              }
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
              onChange={(e) => {
                setReason(e.target.value);
                onDraft(Boolean(e.target.value));
              }}
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
