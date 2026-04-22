"use client";
import { useState } from "react";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Button } from "@/components/ui/Button";
import { Select, Textarea } from "@/components/ui/Input";
import { useInspectionAssignment } from "@/hooks/use-inspection-assignment";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Page } from "@/lib/api";
import type { Technician } from "@/lib/inspection/contracts";
import styles from "./Inspection.module.css";

export function AssignmentForm({
  actor,
  vehicleId,
  version,
  initialChoices,
  onLocked,
  onConfirmed,
  onDraft,
}: {
  actor: CurrentActor;
  vehicleId: string;
  version: number;
  initialChoices: Page<Technician> | null;
  onLocked: (locked: boolean) => void;
  onConfirmed: (id: string) => void;
  onDraft?: (dirty: boolean) => void;
}) {
  const [confirming, setConfirming] = useState(false);
  const assignment = useInspectionAssignment({
    actor,
    vehicleId,
    version,
    initialChoices,
    onLocked,
    onConfirmed,
    onDraft,
  });
  const { choices, technicianId, reason, reading, locked, command } = assignment;

  return (
    <form
      className={styles.form}
      onSubmit={async (event) => {
        event.preventDefault();
        if (command.uncertain) {
          await assignment.submit();
          return;
        }
        setConfirming(true);
      }}
    >
      <ConfirmationDialog
        open={confirming}
        title={
          actor.role === "WorkshopTechnician"
            ? "Claim this inspection?"
            : "Confirm inspection assignment"
        }
        confirmLabel="Confirm assignment"
        busy={command.busy}
        onClose={() => setConfirming(false)}
        onConfirm={async () => {
          await assignment.submit();
          setConfirming(false);
        }}
      >
        <p>
          {actor.role === "WorkshopTechnician" && "Assign this vehicle to yourself for inspection."}
          {actor.role !== "WorkshopTechnician" &&
            (version > 0
              ? "Transfer this unsigned inspection to the selected technician using the entered reason."
              : "Assign this vehicle to the selected technician for inspection.")}
        </p>
      </ConfirmationDialog>
      {actor.role === "BranchManager" && (
        <>
          <Select
            label="Workshop technician"
            value={technicianId}
            required
            disabled={locked || reading}
            onChange={(e) => assignment.changeTechnician(e.target.value)}
          >
            <option value="">Choose a technician</option>
            {technicianId && !choices?.items.some((t) => t.id === technicianId) && (
              <option value={technicianId}>Selected technician</option>
            )}
            {choices?.items.map((t) => (
              <option key={t.id} value={t.id}>
                {t.identity}
              </option>
            ))}
          </Select>
          {(!choices || choices.total > choices.pageSize) && (
            <div className={styles.actions}>
              <Button
                size="compact"
                variant="secondary"
                type="button"
                disabled={locked || reading || choices?.page === 1}
                onClick={() => void assignment.load(choices ? choices.page - 1 : 1)}
              >
                {choices ? "Previous technicians" : "Load technicians"}
              </Button>
              {choices && (
                <Button
                  size="compact"
                  variant="secondary"
                  type="button"
                  disabled={locked || reading || choices.page * choices.pageSize >= choices.total}
                  onClick={() => void assignment.load(choices.page + 1)}
                >
                  More technicians
                </Button>
              )}
            </div>
          )}
          <Textarea
            placeholder="Explain the reason for this technician assignment"
            label="Assignment reason"
            required={version > 0}
            maxLength={1000}
            value={reason}
            disabled={locked}
            onChange={(e) => assignment.changeReason(e.target.value)}
          />
        </>
      )}
      {actor.role === "WorkshopTechnician" && (
        <p>Claim this vehicle for your inspection checklist.</p>
      )}
      {command.uncertain && (
        <p role="alert">
          Assignment is unconfirmed. Keep this window open and reconcile the captured request.
        </p>
      )}
      <Button size="compact" type="submit" disabled={command.busy || reading}>
        {command.uncertain ? "Reconcile assignment" : "Confirm assignment"}
      </Button>
    </form>
  );
}
