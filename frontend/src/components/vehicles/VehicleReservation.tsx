"use client";

import { usePaymentMode } from "@/hooks/use-payment-mode";
import Link from "next/link";
import { useEffect, type ComponentProps, type ReactNode } from "react";
import { Button, buttonClassName } from "@/components/ui/Button";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { Icon } from "@/components/ui/Icon";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { useVehicleReservation } from "@/hooks/use-vehicle-reservation";
import { hasIntakeAccess } from "@/lib/auth/actor";
import { authHref, type CustomerAccount } from "@/lib/auth/contracts";
import type { CustomerVehicleAccess } from "@/lib/vehicles/customer-access";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import type { PurchaseQuote } from "@/lib/vehicles/purchase-quote";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { AssistedDepositRequest } from "./AssistedDepositRequest";
import { AssistedVehicleBooking } from "./AssistedVehicleBooking";
import { VehicleTestDrive } from "./VehicleTestDrive";
import styles from "./VehicleReservation.module.css";

export function VehicleReservation({
  detail,
  access,
  promo,
  promoBusy,
  testDrive,
  financing,
  onReserved,
}: {
  detail: PublicVehicleDetail;
  access: CustomerVehicleAccess;
  promo: PurchaseQuote | null;
  promoBusy: boolean;
  testDrive?: ReactNode;
  financing: ReactNode;
  onReserved?: () => void;
}) {
  const actor = useIntakeActor(access.actor ?? undefined);
  useInitialToast(
    access.status >= 500 ? "Your account could not be checked. Retry before reserving." : null,
  );
  if (!actor.active) {
    return null;
  }
  if (access.status === 401) {
    return (
      <div className={styles.reservation}>
        <div className={styles.actions}>
          <Link
            href={authHref("/login", `/vehicles/${detail.listing.id}`)}
            prefetch={false}
            className={buttonClassName("muted", "md")}
          >
            Sign in to reserve this car
          </Link>
          {testDrive}
          {financing}
        </div>
      </div>
    );
  }
  if (access.status === 403 && access.actor && hasIntakeAccess(access.actor)) {
    const staff = access.actor;
    return (
      <AssistedVehicleBooking
        financing={financing}
        renderBooking={(account) => (
          <CustomerReservation
            key={`${account.userId}:${detail.publicationId}`}
            detail={detail}
            account={account}
            actingFor={account.userId}
            promo={promo}
            disabled={!actor.ready || promoBusy}
            testDrive={
              <VehicleTestDrive
                detail={detail}
                access={{ status: 200, actor: staff, account }}
                actingFor={account.userId}
              />
            }
            financing={financing}
            onReserved={onReserved}
          />
        )}
      />
    );
  }
  if (access.status === 403) {
    return (
      <div className={styles.reservation}>
        {testDrive}
        {financing}
        <p className={styles.help}>Reservations require a Customer account.</p>
      </div>
    );
  }
  if (!access.account || !access.actor || access.status !== 200) {
    return (
      <div className={styles.actions}>
        <Button variant="secondary" onClick={() => window.location.reload()}>
          Retry account check
        </Button>
        {testDrive}
        {financing}
      </div>
    );
  }
  if (!access.account.emailVerified) {
    return (
      <div className={styles.reservation}>
        <div className={styles.actions}>
          <Link href="/account" prefetch={false} className={buttonClassName("primary", "md")}>
            Verify email to reserve
          </Link>
          {testDrive}
          {financing}
        </div>
      </div>
    );
  }
  return (
    <CustomerReservation
      key={`${access.actor.userId}:${detail.publicationId}`}
      detail={detail}
      account={access.account}
      promo={promo}
      disabled={!actor.ready || promoBusy}
      testDrive={testDrive}
      financing={financing}
      onReserved={onReserved}
    />
  );
}

// Staff acting for a customer send a deposit request; a customer reserves for themselves.
export function CustomerReservation(props: ComponentProps<typeof OwnReservation>) {
  if (props.actingFor) {
    return (
      <AssistedDepositRequest
        detail={props.detail}
        account={props.account}
        customerId={props.actingFor}
        promo={props.promo}
        disabled={props.disabled}
        testDrive={props.testDrive}
        financing={props.financing}
      />
    );
  }
  return <OwnReservation {...props} />;
}

function OwnReservation({
  detail,
  account,
  actingFor,
  promo,
  disabled,
  testDrive,
  financing,
  onReserved,
}: {
  detail: PublicVehicleDetail;
  account: CustomerAccount;
  // The customer's id when sales staff reserve on their behalf.
  actingFor?: string;
  promo: PurchaseQuote | null;
  disabled: boolean;
  testDrive?: ReactNode;
  financing: ReactNode;
  onReserved?: () => void;
}) {
  const live = usePaymentMode();
  const reservation = useVehicleReservation(detail, account, actingFor);
  const { command } = reservation;
  const receipt = command.receipt;
  const price = promo?.price ?? detail.price;

  useEffect(() => {
    if (receipt) {
      onReserved?.();
    }
  }, [receipt, onReserved]);

  if (receipt) {
    return (
      <div className={styles.confirmedWrap}>
        <section className={styles.receipt} aria-label="Reservation confirmation" tabIndex={-1}>
          <div className={styles.receiptHeader}>
            <span className={styles.receiptTitle}>
              <Icon name="shield-check" />
              <h2>Reservation confirmed</h2>
            </span>
            <span className={styles.holdBadge}>48-hour hold</span>
          </div>
          <dl className={styles.receiptGrid}>
            <div>
              <dt>Reference</dt>
              <dd>{receipt.number}</dd>
            </div>
            <div>
              <dt>Simulated deposit</dt>
              <dd>{formatCurrency(receipt.deposit)}</dd>
            </div>
            <div>
              <dt>Hold expires</dt>
              <dd>{formatMalaysianDateTime(receipt.expiresAt)}</dd>
            </div>
            <div>
              <dt>Promo</dt>
              <dd>{receipt.promoCode ?? "None"}</dd>
            </div>
          </dl>
          <p className={styles.receiptNote}>
            {actingFor
              ? "Deposit counts toward the customer's cash down payment. The receipt is emailed to the customer."
              : "Deposit counts toward your cash down payment. Receipt emailed separately."}
          </p>
        </section>
        <div className={styles.actions}>
          {!actingFor && (
            <Link href="/account" prefetch={false} className={buttonClassName("primary", "md")}>
              Manage reservation in account
            </Link>
          )}
          {financing}
        </div>
      </div>
    );
  }

  return (
    <div className={styles.reservation}>
      <div className={styles.actions}>
        <Button
          size={actingFor ? "compact" : "md"}
          disabled={disabled || reservation.locked}
          onClick={() => reservation.open(promo?.promoId ?? null)}
        >
          Reserve this car
        </Button>
        {testDrive}
        {financing}
      </div>
      <ConfirmationDialog
        open={Boolean(reservation.confirmation)}
        title="Reserve this car?"
        confirmLabel={
          command.uncertain
            ? "Reconcile reservation"
            : `Confirm RM500 ${live ? "deposit" : "simulated deposit"}`
        }
        cancelLabel="Keep browsing"
        busy={command.busy}
        locked={command.uncertain}
        onClose={reservation.close}
        onConfirm={reservation.reserve}
        facts={[
          {
            label: "Vehicle",
            value: `${detail.listing.manufactureYear} ${detail.listing.make} ${detail.listing.model} ${detail.listing.trim}`,
          },
          { label: "Buyer", value: account.legalName },
          { label: "Confirmation email", value: account.email },
          { label: "Estimated total", value: formatCurrency(price.total) },
          { label: "Accepted promo", value: promo?.promoCode ?? "None" },
        ]}
      >
        <p>
          {live
            ? "This holds the car for 15 minutes while you pay a refundable RM500 deposit securely online. The reservation is made only once your payment is confirmed."
            : "This reservation records a refundable RM500 deposit and holds the car for 48 hours. No real payment is taken."}{" "}
          The deposit counts toward your cash contribution.
        </p>
        <p>
          The backend confirms availability and current terms. Your receipt shows the reserved total
          and exact expiry. Cancellation or expiry records one full refund.
        </p>
      </ConfirmationDialog>
    </div>
  );
}
