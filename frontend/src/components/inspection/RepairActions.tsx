"use client";
import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Input";
import { useApiCommand } from "@/hooks/use-api-command";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseApproval, parseCompletion } from "@/lib/inspection/parse-report";
import { isRecord } from "@/lib/auth/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Draft, RepairRow } from "@/lib/inspection/contracts";
import type { Approval, Completion } from "@/lib/inspection/report-contracts";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function RepairActions({
  actor,
  draft,
  state,
  disabled,
  onLock,
  onConfirmed,
  onDraft,
}: {
  actor: CurrentActor;
  draft: Draft;
  state: RepairRow;
  disabled: boolean;
  onLock: (v: boolean) => void;
  onConfirmed: () => Promise<void>;
  onDraft: (dirty: boolean) => void;
}) {
  const [notes, setNotes] = useState("");
  const manager = actor.role === "BranchManager";
  const action = manager ? "repair-approvals" : "repair-completions";
  const parse = (v: unknown): Approval | Completion | null =>
    manager ? parseApproval(v) : parseCompletion(v);
  const command = useApiCommand(
    `drafts/${draft.id}/${action}`,
    parse,
    (r, body) =>
      isRecord(body) &&
      r.operationId === body.operationId &&
      r.inspectionId === draft.id &&
      r.actorId === actor.userId &&
      r.requestId === body.requestId &&
      r.assignmentVersion === body.assignmentVersion &&
      r.revision === body.revision &&
      r.repairVersion === body.repairVersion,
    inspectionFetch,
  );
  const actionable = manager
    ? state.status === "PendingApproval"
    : actor.role === "WorkshopTechnician" &&
      ["Approved", "ApprovalNotRequired"].includes(state.status);
  const actionLabel = manager ? "Approve repair budget" : "Confirm repairs completed";
  if (!actionable) {
    return null;
  }
  return (
    <form
      className={workbench.protocol}
      onSubmit={async (event) => {
        event.preventDefault();
        onLock(true);
        const result = await command.submit({
          operationId: crypto.randomUUID(),
          requestId: state.requestId,
          assignmentVersion: state.assignmentVersion,
          revision: state.revision,
          repairVersion: state.repairVersion,
          ...(!manager ? { notes: notes.trim() } : {}),
        });
        if (result) {
          onLock(false);
        }
        if (result?.status === 200 && result.data) {
          onDraft(false);
          await onConfirmed();
        }
      }}
    >
      {manager ? (
        <p>Approve the exact work and requested budget shown above.</p>
      ) : (
        <Textarea
          label="Completion & reinspection notes"
          required
          maxLength={1000}
          rows={3}
          disabled={command.busy || command.uncertain || disabled}
          value={notes}
          onChange={(e) => {
            setNotes(e.target.value);
            onDraft(Boolean(e.target.value));
          }}
        />
      )}
      {command.uncertain && (
        <p role="alert">
          {manager ? "Approval" : "Completion"} is unconfirmed. Reconcile the captured request
          before any further action.
        </p>
      )}
      <div className={workbench.actionFooter}>
        <Button
          size="compact"
          type="submit"
          disabled={command.busy || Boolean(command.receipt) || (disabled && !command.uncertain)}
        >
          {command.uncertain ? "Reconcile repair action" : actionLabel}
        </Button>
      </div>
    </form>
  );
}
