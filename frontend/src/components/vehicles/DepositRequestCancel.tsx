"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { useApiCommand } from "@/hooks/use-api-command";
import { privateApiFetch } from "@/lib/api-browser";
import {
  matchesDepositRequestCancellation,
  parseDepositRequestCancellation,
} from "@/lib/vehicles/deposit-request-cancellation";

// Staff release a car they asked a deposit for. Nothing was paid, so nothing is refunded, and the emailed link stops
// working; a new request can be sent afterwards.
export function DepositRequestCancel({
  requestId,
  onChanged,
}: {
  requestId: string;
  onChanged: () => void;
}) {
  const [confirming, setConfirming] = useState(false);
  const command = useApiCommand(
    `/api/reservation-requests/${requestId}/cancellations`,
    parseDepositRequestCancellation,
    (receipt) => matchesDepositRequestCancellation(receipt, requestId),
    privateApiFetch,
  );
  const locked = command.busy || command.uncertain;

  async function cancel() {
    const result = await command.submit({});
    if (result?.data) {
      setConfirming(false);
      onChanged();
    }
  }

  return (
    <>
      <Button
        size="compact"
        variant="secondary"
        disabled={locked}
        onClick={() => setConfirming(true)}
      >
        Cancel request
      </Button>
      <ConfirmationDialog
        open={confirming}
        title="Cancel this deposit request?"
        confirmLabel={command.uncertain ? "Reconcile cancellation" : "Cancel request"}
        cancelLabel="Keep request"
        danger
        busy={command.busy}
        locked={command.uncertain}
        onClose={() => setConfirming(false)}
        onConfirm={cancel}
      >
        <p>
          The car is released at once and the link in the customer&apos;s email stops working.
          Nothing was paid, so nothing is refunded. You can send a new request afterwards.
        </p>
      </ConfirmationDialog>
    </>
  );
}
