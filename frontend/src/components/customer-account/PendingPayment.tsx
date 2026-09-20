"use client";

import { buttonClassName, Button } from "@/components/ui/Button";
import { useApiCommand } from "@/hooks/use-api-command";
import { privateApiFetch } from "@/lib/api-browser";
import { isRecord } from "@/lib/auth/contracts";
import { formatCurrency } from "@/lib/currency-format";
import { RESERVATION_DEPOSIT } from "@/lib/vehicles/deposit-terms";
import type { PendingPayment as PendingPaymentItem } from "@/lib/vehicles/account-reservations";
import { HoldCountdown } from "./HoldCountdown";
import styles from "./PendingPayment.module.css";

function parseCancellation(value: unknown) {
  return isRecord(value) && typeof value.requestId === "string"
    ? { requestId: value.requestId }
    : null;
}

// The deposit was started but not paid: the car is held briefly and nothing is reserved until the payment clears.
export function PendingPayment({
  item,
  disabled,
  onChanged,
}: {
  item: PendingPaymentItem;
  disabled: boolean;
  onChanged: () => void;
}) {
  const command = useApiCommand(
    `/api/account/pending-payments/${item.requestId}/cancellations`,
    parseCancellation,
    (receipt) => receipt.requestId === item.requestId,
    privateApiFetch,
  );

  async function giveUp() {
    const result = await command.submit({});
    if (result && (result.status === 200 || result.status === 404 || result.status === 409)) {
      onChanged();
    }
  }

  return (
    <article className={styles.pending} aria-label="Payment waiting">
      <div className={styles.text}>
        <p className={styles.state}>Payment not finished</p>
        <h3>
          {item.manufactureYear} {item.make} {item.model} {item.trim}
        </h3>
        <p className={styles.help}>
          {item.branchName} · The {formatCurrency(RESERVATION_DEPOSIT, "RM")} deposit has not been
          paid, so this car is held for you but not yet reserved.
        </p>
        <div className={styles.deadline}>
          <HoldCountdown until={item.expiresAt} label="left to pay before the hold is released" />
        </div>
      </div>
      <div className={styles.actions}>
        {item.paymentUrl ? (
          <a href={item.paymentUrl} className={buttonClassName("primary", "lg")}>
            Continue to payment
          </a>
        ) : null}
        <Button variant="ghost" disabled={disabled || command.busy} onClick={() => void giveUp()}>
          Cancel this payment
        </Button>
      </div>
    </article>
  );
}
