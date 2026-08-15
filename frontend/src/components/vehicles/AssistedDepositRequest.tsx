"use client";

import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Icon } from "@/components/ui/Icon";
import { useDepositRequest } from "@/hooks/use-deposit-request";
import type { CustomerAccount } from "@/lib/auth/contracts";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { PurchaseQuote } from "@/lib/vehicles/purchase-quote";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import styles from "./VehicleReservation.module.css";

// Staff reserving for a customer send a deposit request instead of taking the deposit themselves: the car is held
// for 24 hours, and the customer's confirmation by email makes the reservation and sends the ordinary receipt.
export function AssistedDepositRequest({
  detail,
  account,
  customerId,
  promo,
  disabled,
  testDrive,
  financing,
}: {
  detail: PublicVehicleDetail;
  account: CustomerAccount;
  customerId: string;
  promo: PurchaseQuote | null;
  disabled: boolean;
  testDrive?: ReactNode;
  financing: ReactNode;
}) {
  const request = useDepositRequest(detail, customerId);
  const { command } = request;
  const receipt = command.receipt;
  const price = promo?.price ?? detail.price;

  if (receipt) {
    return (
      <div className={styles.confirmedWrap}>
        <section className={styles.receipt} aria-label="Deposit request sent" tabIndex={-1}>
          <div className={styles.receiptHeader}>
            <span className={styles.receiptTitle}>
              <Icon name="shield-check" />
              <h2>Deposit request sent</h2>
            </span>
            <span className={styles.holdBadge}>24-hour hold</span>
          </div>
          <dl className={styles.receiptGrid}>
            <div>
              <dt>Customer</dt>
              <dd>{account.legalName}</dd>
            </div>
            <div>
              <dt>Held until</dt>
              <dd>{formatMalaysianDateTime(receipt.expiresAt)}</dd>
            </div>
          </dl>
          <p className={styles.receiptNote}>
            The customer is emailed a link to confirm the simulated deposit. The car is reserved and
            the receipt is sent when they confirm. If they do not, the hold is released.
          </p>
        </section>
        <div className={styles.actions}>{financing}</div>
      </div>
    );
  }

  return (
    <div className={styles.reservation}>
      <div className={styles.actions}>
        <Button
          size="compact"
          disabled={disabled || request.locked}
          onClick={() => request.open(promo?.promoId ?? null)}
        >
          Send deposit request
        </Button>
        {testDrive}
        {financing}
      </div>
      <ConfirmationDialog
        open={Boolean(request.confirmation)}
        title="Send a deposit request?"
        confirmLabel={command.uncertain ? "Reconcile request" : "Send deposit request"}
        cancelLabel="Keep browsing"
        busy={command.busy}
        locked={command.uncertain}
        onClose={request.close}
        onConfirm={request.send}
        facts={[
          {
            label: "Vehicle",
            value: `${detail.listing.manufactureYear} ${detail.listing.make} ${detail.listing.model} ${detail.listing.trim}`,
          },
          { label: "Customer", value: account.legalName },
          { label: "Request emailed to", value: account.email },
          { label: "Estimated total", value: formatCurrency(price.total) },
          { label: "Accepted promo", value: promo?.promoCode ?? "None" },
        ]}
      >
        <p>
          The customer is emailed a link to confirm the refundable RM500 simulated deposit. No real
          payment is taken. The car is held for them for 24 hours.
        </p>
        <p>
          The reservation is made, and its receipt sent, only when they confirm. If they do not, the
          hold is released and nothing is recorded.
        </p>
      </ConfirmationDialog>
    </div>
  );
}
