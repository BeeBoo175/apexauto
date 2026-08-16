"use client";

import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { useApiCommand } from "@/hooks/use-api-command";
import { parseCancellation } from "@/lib/intake/parse";
import type { Appointment } from "@/lib/intake/contracts";
import styles from "./CancelAppointment.module.css";

export function CancelAppointment({
  appointment,
  onClose,
  onConfirmed,
}: {
  appointment: Appointment;
  onClose: () => void;
  onConfirmed: (id: string, cancelledAt: string) => void;
}) {
  const command = useApiCommand(
    `/api/intake/appointments/${appointment.id}/cancellations`,
    parseCancellation,
    (receipt) => receipt.appointmentId === appointment.id,
  );
  async function cancel() {
    const result = await command.submit({});
    if (result?.status !== 200 || !result.data) {
      return;
    }
    onConfirmed(result.data.appointmentId, result.data.cancelledAt);
    onClose();
  }
  return (
    <ConfirmationDialog
      open
      title="Cancel this appointment?"
      onClose={onClose}
      busy={command.busy}
      locked={command.uncertain}
      danger
      cancelLabel="Keep appointment"
      confirmLabel={command.uncertain ? "Retry same cancellation" : "Confirm cancellation"}
      onConfirm={cancel}
    >
      <div className={styles.content}>
        <p>
          {appointment.registrationPlate} at {appointment.branchName}
        </p>
        <p className={styles.description}>
          Cancellation is available before the appointment starts and before check-in. Your history
          is retained. A new booking starts a new intake.
        </p>
      </div>
    </ConfirmationDialog>
  );
}
