"use client";

import { useId, type ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import {
  useStaffTestDriveCommand,
  testDriveCommandLabels,
} from "@/hooks/use-staff-test-drive-command";
import type { CurrentActor } from "@/lib/auth/actor";
import type { StaffTestDriveDetail } from "@/lib/test-drives/staff-query";
import type { TestDriveStaffCommand } from "@/lib/test-drives/staff-commands";
import { formatMalaysianDateTime } from "@/lib/date-format";
import shared from "@/components/staff/StaffWorkspace.module.css";
import styles from "./StaffTestDrive.module.css";

export function StaffTestDriveActions({
  actor,
  detail,
  refresh,
  reading,
  children,
}: {
  actor: CurrentActor;
  detail: StaffTestDriveDetail;
  refresh: (kind?: TestDriveStaffCommand) => Promise<boolean>;
  reading: boolean;
  children: ReactNode;
}) {
  const formId = useId();
  const workflow = useStaffTestDriveCommand(actor, detail, refresh);
  const { action, captured, command, locked } = workflow;
  const handling = action === "starts" || action === "outcomes";
  const reasonRequired =
    action === "cancellations" ||
    (action === "assignments" && Boolean(detail.booking.handlerId)) ||
    (action === "outcomes" && workflow.outcome === "NotCompleted");
  const disabled = locked || reading || workflow.needsRefresh;
  const choices = workflow.actions;
  const selectedAllowed = !action || choices.includes(action);

  return (
    <>
      <LoadingProgress
        label="Updating test drive"
        active={command.busy || workflow.readingHandlers}
      />
      <div className={`${shared.body} ${styles.detailBody}`}>
        {children}
        {workflow.confirmed && (
          <dl className={styles.receipt}>
            <div>
              <dt>Confirmed action</dt>
              <dd>{testDriveCommandLabels[workflow.confirmed.kind]}</dd>
            </div>
            <div>
              <dt>Reference</dt>
              <dd>{String(workflow.confirmed.body.operationId)}</dd>
            </div>
          </dl>
        )}
        {workflow.needsRefresh && (
          <Button
            size="compact"
            variant="secondary"
            disabled={reading || locked}
            onClick={() => void workflow.reconcileRead()}
          >
            Refresh confirmed test drive
          </Button>
        )}
        {(choices.length > 0 || workflow.dirty || captured) && (
          <form
            id={formId}
            className={styles.actions}
            onSubmit={(event) => {
              event.preventDefault();
              workflow.prepare();
            }}
          >
            <Select
              label="Test drive action"
              density="compact"
              value={action}
              disabled={disabled}
              onChange={(event) =>
                workflow.chooseAction(event.target.value as TestDriveStaffCommand | "")
              }
            >
              <option value="">Choose an action</option>
              {!selectedAllowed && (
                <option value={action}>
                  {testDriveCommandLabels[action as TestDriveStaffCommand]} (refresh required)
                </option>
              )}
              {choices.map((kind) => (
                <option key={kind} value={kind}>
                  {testDriveCommandLabels[kind]}
                </option>
              ))}
            </Select>
            {action === "assignments" && (
              <div className={shared.stack}>
                <Select
                  label="Handling staff"
                  density="compact"
                  value={workflow.handlerId}
                  disabled={disabled || workflow.readingHandlers}
                  required
                  onChange={(event) => workflow.setHandlerId(event.target.value)}
                >
                  <option value="">Choose a handler</option>
                  {workflow.handlers?.items.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.name}
                    </option>
                  ))}
                </Select>
                <Button
                  size="compact"
                  variant="secondary"
                  disabled={disabled || workflow.readingHandlers}
                  onClick={() => void workflow.loadHandlers()}
                >
                  {workflow.readingHandlers ? "Loading handlers…" : "Refresh handlers"}
                </Button>
              </div>
            )}
            {action === "outcomes" && (
              <Select
                label="Outcome"
                density="compact"
                value={workflow.outcome}
                disabled={disabled}
                onChange={(event) =>
                  workflow.setOutcome(event.target.value as "Completed" | "NotCompleted")
                }
              >
                {detail.start && <option value="Completed">Completed</option>}
                <option value="NotCompleted">Attended, not completed</option>
              </Select>
            )}
            {handling && (
              <Select
                label="Customer attendance"
                density="compact"
                value={workflow.present ? "present" : ""}
                disabled={disabled}
                required
                onChange={(event) => workflow.setPresent(event.target.value === "present")}
              >
                <option value="">Confirm attendance</option>
                <option value="present">The customer is present</option>
              </Select>
            )}
            {action === "starts" && (
              <>
                <Select
                  label="Driving licence check"
                  density="compact"
                  value={workflow.licence ? "checked" : ""}
                  disabled={disabled}
                  required
                  onChange={(event) => workflow.setLicence(event.target.value === "checked")}
                >
                  <option value="">Confirm licence check</option>
                  <option value="checked">I checked a valid driving licence</option>
                </Select>
                <p className={shared.help}>
                  Record the check only. Do not enter or upload a licence number or image.
                </p>
              </>
            )}
            {reasonRequired && (
              <Input
                label="Reason"
                value={workflow.reason}
                maxLength={1000}
                disabled={disabled}
                required
                onChange={(event) => workflow.setReason(event.target.value)}
              />
            )}
            {action === "outcomes" && (
              <Input
                label="Outcome notes (optional)"
                value={workflow.notes}
                maxLength={1000}
                disabled={disabled}
                onChange={(event) => workflow.setNotes(event.target.value)}
              />
            )}
            {action === "starts" && (
              <p className={shared.help}>
                Start on the booked Malaysian date. The vehicle and handler must be free for the
                next 30 minutes. An unfinished drive stays active until an outcome is recorded.
              </p>
            )}
          </form>
        )}
      </div>
      {(choices.length > 0 || workflow.dirty || captured) && (
        <footer className={shared.footer}>
          <div className={shared.footerContext}>
            <strong>{action ? testDriveCommandLabels[action] : "Test drive handling"}</strong>
            <span>
              {command.uncertain
                ? "Reconcile the original request before continuing."
                : "Only confirmed actions update the recorded test drive."}
            </span>
          </div>
          <div className={shared.footerActions}>
            {workflow.dirty && (
              <Button
                size="compact"
                variant="secondary"
                disabled={locked}
                onClick={workflow.discard}
              >
                Discard changes
              </Button>
            )}
            {command.uncertain ? (
              <Button size="compact" disabled={command.busy} onClick={() => void workflow.submit()}>
                Reconcile same action
              </Button>
            ) : (
              <Button
                size="compact"
                type="submit"
                form={formId}
                disabled={disabled || !action || !selectedAllowed}
              >
                {action ? testDriveCommandLabels[action] : "Confirm action"}
              </Button>
            )}
          </div>
        </footer>
      )}
      <ConfirmationDialog
        open={Boolean(captured)}
        title={captured ? testDriveCommandLabels[captured.kind] : "Confirm test drive action"}
        confirmLabel={command.uncertain ? "Reconcile same action" : "Confirm action"}
        danger={captured?.kind === "cancellations"}
        busy={command.busy}
        locked={command.uncertain}
        onClose={workflow.closeConfirmation}
        onConfirm={workflow.submit}
        facts={[
          { label: "Vehicle", value: detail.booking.registrationPlate },
          { label: "Customer", value: detail.booking.customerName },
          { label: "Booked time", value: formatMalaysianDateTime(detail.booking.startsAt) },
          ...(captured?.body.reason
            ? [{ label: "Reason", value: String(captured.body.reason) }]
            : []),
          ...(captured?.kind === "assignments"
            ? [
                {
                  label: "Handler",
                  value:
                    workflow.handlers?.items.find((item) => item.id === captured.body.handlerId)
                      ?.name ?? String(captured.body.handlerId),
                },
              ]
            : []),
          ...(captured?.kind === "outcomes"
            ? [
                { label: "Outcome", value: String(captured.body.kind) },
                { label: "Notes", value: String(captured.body.notes ?? "None") },
              ]
            : []),
        ]}
      >
        <p>
          {captured?.kind === "starts"
            ? "Confirm that the customer is present and a valid driving licence has been checked. Starting exclusively occupies this vehicle until a deliberate outcome."
            : "Confirm this action for the selected test drive. Its recorded history is retained."}
        </p>
      </ConfirmationDialog>
    </>
  );
}
