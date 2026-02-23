"use client";

import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { useApiCommand } from "@/hooks/use-api-command";
import { parseCancellation } from "@/lib/intake/parse";
import type { Appointment } from "@/lib/intake/contracts";

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
    `/api/intake/appointments/${appointment.id}/cancellation`,
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
    <Dialog
      open
      title="Cancel this appointment?"
      onClose={onClose}
      closeDisabled={command.busy || command.uncertain}
    >
      <div className="flex flex-col gap-5">
        <p>
          {appointment.registrationPlate} at {appointment.branchName}
        </p>
        <p className="text-sm text-muted">
          Cancellation is available before the appointment starts and before check-in. Your history
          is retained. A new booking starts a new intake.
        </p>
        <Button onClick={() => void cancel()} disabled={command.busy}>
          {command.uncertain ? "Retry same cancellation" : "Confirm cancellation"}
        </Button>
      </div>
    </Dialog>
  );
}
