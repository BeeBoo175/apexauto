"use client";

import { useLayoutEffect } from "react";
import { Button } from "@/components/ui/Button";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { Select, Textarea } from "@/components/ui/Input";
import { useIntakeOwnership } from "@/hooks/use-intake-ownership";
import type { CurrentActor } from "@/lib/auth/actor";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { AppointmentDetail } from "@/lib/staff/contracts";
import styles from "./StaffWorkspace.module.css";
import record from "./StaffRecordContent.module.css";

export function IntakeOwnership({
  actor,
  detail,
  disabled,
  onGuarded,
  onChanged,
}: {
  actor: CurrentActor;
  detail: AppointmentDetail;
  disabled: boolean;
  onGuarded: (guarded: boolean) => void;
  onChanged: () => Promise<void>;
}) {
  const ownership = useIntakeOwnership({ actor, detail, onChanged });
  const { command, locked, dirty } = ownership;
  const current = detail.ownership;
  const manager = actor.role === "BranchManager";
  const agent = actor.role === "SalesAgent";
  const available = detail.status === "Booked" && (manager || agent);
  const assigned = Boolean(current?.handlerId);
  const own = current?.handlerId === actor.userId;
  const guarded = locked || dirty;
  const blocked = disabled || locked;
  useLayoutEffect(() => {
    onGuarded(guarded);
    return () => onGuarded(false);
  }, [guarded, onGuarded]);

  return (
    <section className={styles.stack} aria-label="Intake ownership">
      <h3 className={record.title}>Handling agent</h3>
      <dl className={record.facts}>
        <div>
          <dt>Handling agent</dt>
          <dd>{current?.handlerEmail ?? "Unassigned"}</dd>
        </div>
        {current && (
          <div>
            <dt>Ownership updated</dt>
            <dd>{formatMalaysianDateTime(current.recordedAt)}</dd>
          </div>
        )}
      </dl>
      {assigned && !current?.handlerEligible && (
        <p className={styles.help}>
          The recorded handler is no longer eligible. A branch manager must reassign this
          appointment.
        </p>
      )}
      {current?.reason && <p className={styles.help}>Handoff reason: {current.reason}</p>}
      {available && manager && !command.uncertain && (
        <div className={styles.stack}>
          <Select
            label="Assign handling agent"
            value={ownership.handlerId}
            disabled={blocked || ownership.reading}
            onChange={(event) => ownership.setHandlerId(event.target.value)}
          >
            <option value="">Choose a handler</option>
            {ownership.handlerId &&
              !ownership.choices?.some((item) => item.id === ownership.handlerId) && (
                <option value={ownership.handlerId}>Selected handler</option>
              )}
            {ownership.choices?.map((item) => (
              <option key={item.id} value={item.id}>
                {item.email}
              </option>
            ))}
          </Select>
          <Button
            size="compact"
            variant="secondary"
            disabled={blocked || ownership.reading}
            onClick={() => void ownership.loadHandlers()}
          >
            {ownership.choices ? "Refresh handlers" : "Load handlers"}
          </Button>
          <Textarea
            label="Handoff reason"
            placeholder="Explain the reassignment"
            maxLength={1000}
            value={ownership.reason}
            disabled={blocked}
            onChange={(event) => ownership.setReason(event.target.value)}
          />
          <p className={styles.help}>
            Reassignment requires a reason. Select yourself to personally check in. Unsaved check-in
            entries are never transferred.
          </p>
        </div>
      )}
      {command.uncertain ? (
        <Button
          size="compact"
          disabled={command.busy || disabled}
          onClick={() => void ownership.submit(ownership.action)}
        >
          Reconcile ownership
        </Button>
      ) : (
        <div className={styles.footerActions}>
          {dirty && (
            <ConfirmationButton
              size="compact"
              variant="secondary"
              disabled={blocked}
              confirmationTitle="Discard ownership entries?"
              message="Clear your selected handler and handoff reason."
              onConfirm={ownership.discard}
            >
              Discard ownership entries
            </ConfirmationButton>
          )}
          {available && agent && !assigned && (
            <ConfirmationButton
              size="compact"
              disabled={blocked}
              confirmationTitle="Claim this appointment?"
              message={`Become the handling agent for ${detail.registrationPlate}.`}
              onConfirm={() => ownership.submit("claims")}
            >
              Claim appointment
            </ConfirmationButton>
          )}
          {available && manager && (
            <ConfirmationButton
              size="compact"
              disabled={
                blocked ||
                ownership.reading ||
                !ownership.handlerId ||
                ownership.handlerId === current?.handlerId ||
                (assigned && !ownership.reason.trim())
              }
              confirmationTitle="Confirm handling agent?"
              message={`Assign ${detail.registrationPlate} to the selected handler.`}
              onConfirm={() => ownership.submit("assignments")}
            >
              Assign handler
            </ConfirmationButton>
          )}
          {available && assigned && (manager || own) && (
            <ConfirmationButton
              size="compact"
              variant="secondary"
              disabled={blocked}
              confirmationTitle="Release this appointment?"
              message="Return this appointment to the branch claim queue."
              onConfirm={() => ownership.submit("releases")}
            >
              Release appointment
            </ConfirmationButton>
          )}
        </div>
      )}
    </section>
  );
}
