"use client";

import { usePaymentMode } from "@/hooks/use-payment-mode";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Icon } from "@/components/ui/Icon";
import { useDepositRequest } from "@/hooks/use-deposit-request";
import type { CustomerAccount } from "@/lib/auth/contracts";
import { formatCurrency } from "@/lib/currency-format";
import { DEPOSIT_REQUEST_HOURS, RESERVATION_DEPOSIT } from "@/lib/vehicles/deposit-terms";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { PurchaseQuote } from "@/lib/vehicles/purchase-quote";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import styles from "./VehicleReservation.module.css";

// Staff request the deposit by email; only customer confirmation creates the reservation.
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
  const live = usePaymentMode();
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
            <span className={styles.holdBadge}>{DEPOSIT_REQUEST_HOURS}-hour hold</span>
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
            The customer is emailed a link to {live ? "pay" : "confirm"} the{" "}
            {live ? "" : "simulated "}deposit. The car is reserved and the receipt is sent when they
            confirm. If they do not, the hold is released.
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
        cancelLabel="Go back"
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
          {live
            ? `The customer is emailed a link to pay the refundable ${formatCurrency(RESERVATION_DEPOSIT, "RM")} deposit securely online.`
            : `The customer is emailed a link to confirm the refundable ${formatCurrency(RESERVATION_DEPOSIT, "RM")} simulated deposit. No real payment is taken.`}{" "}
          The car is held for them for {DEPOSIT_REQUEST_HOURS} hours.
        </p>
        <p>
          The reservation is made, and its receipt sent, only when they confirm. If they do not, the
          hold is released and nothing is recorded.
        </p>
      </ConfirmationDialog>
    </div>
  );
}
