"use client";

import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { useApiCommand } from "@/hooks/use-api-command";
import { privateApiFetch } from "@/lib/api-browser";
import { formatCurrency } from "@/lib/currency-format";
import {
  reservationVehicleName,
  type AccountReservation,
} from "@/lib/vehicles/account-reservations";
import {
  matchesCustomerCancellation,
  parseCustomerCancellation,
  type CustomerReservationCancellation,
} from "@/lib/vehicles/customer-reservation-cancellation";

export function CancelReservation({
  item,
  actorId,
  onClose,
  onConfirmed,
  onChanged,
}: {
  item: AccountReservation;
  actorId: string;
  onClose: () => void;
  onConfirmed: (item: AccountReservation, receipt: CustomerReservationCancellation) => void;
  onChanged: () => void;
}) {
  const command = useApiCommand(
    `/api/account/reservations/${item.receipt.id}/cancellations`,
    parseCustomerCancellation,
    (receipt, body) => matchesCustomerCancellation(receipt, body, item.receipt.id, actorId),
    privateApiFetch,
  );

  async function cancel() {
    const result = await command.submit(
      command.uncertain ? null : { operationId: crypto.randomUUID(), reason: null },
    );
    if (result?.status === 200 && result.data) {
      onConfirmed(item, result.data);
      onClose();
    }
    if (result?.status === 409 || result?.status === 404) {
      onClose();
      onChanged();
    }
  }

  return (
    <ConfirmationDialog
      open
      title="Cancel this reservation?"
      facts={[
        { label: "Vehicle", value: reservationVehicleName(item) },
        { label: "Simulated refund", value: formatCurrency(item.receipt.deposit) },
      ]}
      cancelLabel="Keep reservation"
      confirmLabel={command.uncertain ? "Reconcile cancellation" : "Confirm cancellation"}
      danger
      busy={command.busy}
      locked={command.uncertain}
      onClose={onClose}
      onConfirm={cancel}
    >
      <p>
        This releases your hold and records a full simulated deposit refund. No real money moves.
      </p>
      {command.uncertain && (
        <p>The cancellation result is unconfirmed. Reconcile the same request before leaving.</p>
      )}
    </ConfirmationDialog>
  );
}
