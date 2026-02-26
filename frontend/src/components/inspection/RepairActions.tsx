"use client";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Input";
import { useInspectionRepairAction } from "@/hooks/use-inspection-repair-action";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Draft, RepairRow } from "@/lib/inspection/contracts";
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
  const repair = useInspectionRepairAction({ actor, draft, state, onLock, onConfirmed, onDraft });
  const { notes, manager, actionable, command } = repair;
  const actionLabel = manager ? "Approve repair budget" : "Confirm repairs completed";

  if (!actionable) {
    return null;
  }
  return (
    <form
      className={workbench.protocol}
      onSubmit={async (event) => {
        event.preventDefault();
        await repair.submit();
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
          onChange={(e) => repair.changeNotes(e.target.value)}
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
