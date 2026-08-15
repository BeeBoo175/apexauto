"use client";

import { useState } from "react";
import { CustomerAvatar } from "@/components/staff/CustomerAvatar";
import { HoldTimer } from "@/components/staff/HoldTimer";
import { Button } from "@/components/ui/Button";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { useApiCommand } from "@/hooks/use-api-command";
import { useMinuteClock } from "@/hooks/use-minute-clock";
import { privateApiFetch } from "@/lib/api-browser";
import {
  matchesDepositRequestCancellation,
  parseDepositRequestCancellation,
} from "@/lib/vehicles/deposit-request-cancellation";
import { DEPOSIT_REQUEST_HOURS } from "@/lib/vehicles/deposit-request";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import styles from "./ListingPublication.module.css";

// A car held by a deposit request until its customer confirms: the customer, the time left and a way to release it.
export function ListingPendingDeposit({
  pending,
  onChanged,
}: {
  pending: NonNullable<ListingWorkspace["pendingDeposit"]>;
  onChanged: () => void;
}) {
  const [now] = useMinuteClock();
  const [confirming, setConfirming] = useState(false);
  const command = useApiCommand(
    `/api/reservation-requests/${pending.requestId}/cancellations`,
    parseDepositRequestCancellation,
    (receipt) => matchesDepositRequestCancellation(receipt, pending.requestId),
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
    <div className={styles.pendingDeposit}>
      <span>
        <CustomerAvatar id={pending.customerId} name={pending.customer} />
        {" · awaiting deposit confirmation"}
      </span>
      <HoldTimer
        dueAt={pending.expiresAtUtc}
        now={now}
        totalSeconds={DEPOSIT_REQUEST_HOURS * 3600}
        compact
      />
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
    </div>
  );
}
