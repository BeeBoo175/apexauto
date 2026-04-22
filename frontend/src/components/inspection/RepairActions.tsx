"use client";
import { useId, useState } from "react";
import { createPortal } from "react-dom";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Input";
import { useInspectionRepairAction } from "@/hooks/use-inspection-repair-action";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Draft, RepairRow } from "@/lib/inspection/contracts";
import { formatCurrency } from "@/lib/currency-format";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function RepairActions({
  actor,
  draft,
  state,
  disabled,
  onLock,
  onConfirmed,
  onDraft,
  active = true,
  actionsContainer,
}: {
  actor: CurrentActor;
  draft: Draft;
  state: RepairRow;
  disabled: boolean;
  onLock: (v: boolean) => void;
  onConfirmed: () => Promise<void>;
  onDraft: (dirty: boolean) => void;
  active?: boolean;
  actionsContainer?: HTMLElement | null;
}) {
  const repair = useInspectionRepairAction({ actor, draft, state, onLock, onConfirmed, onDraft });
  const { notes, manager, actionable, command } = repair;
  const actionLabel = manager ? "Approve budget" : "Complete repairs";
  const formId = useId();
  const [confirming, setConfirming] = useState(false);

  if (!actionable || !active) {
    return null;
  }
  const submit = (
    <Button
      size="compact"
      type="submit"
      form={formId}
      disabled={command.busy || Boolean(command.receipt) || (disabled && !command.uncertain)}
    >
      {command.uncertain ? "Reconcile action" : actionLabel}
    </Button>
  );
  return (
    <form
      id={formId}
      className={workbench.protocol}
      onSubmit={async (event) => {
        event.preventDefault();
        if (command.uncertain) {
          await repair.submit();
          return;
        }
        setConfirming(true);
      }}
    >
      <ConfirmationDialog
        open={confirming}
        title={manager ? "Approve this repair budget?" : "Complete these repairs?"}
        confirmLabel={actionLabel}
        busy={command.busy}
        facts={[
          { label: "Vehicle", value: state.registrationPlate },
          { label: "Repair budget", value: formatCurrency(state.budget) },
        ]}
        onClose={() => setConfirming(false)}
        onConfirm={async () => {
          await repair.submit();
          setConfirming(false);
        }}
      >
        {manager ? (
          <p>Approve this budget for the work listed in the repair request.</p>
        ) : (
          <p>Record these repairs as complete with your completion and reinspection notes.</p>
        )}
      </ConfirmationDialog>
      {manager ? (
        <p>Approve the exact work and requested budget shown above.</p>
      ) : (
        <Textarea
          label="Completion & reinspection notes"
          placeholder="Describe the repairs completed and the reinspection results"
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
      {actionsContainer ? createPortal(submit, actionsContainer) : submit}
    </form>
  );
}
