"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { Button, buttonClassName } from "@/components/ui/Button";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { useVehicleReservation } from "@/hooks/use-vehicle-reservation";
import { authHref, type CustomerAccount } from "@/lib/auth/contracts";
import type { CustomerVehicleAccess } from "@/lib/vehicles/customer-access";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import type { PurchaseQuote } from "@/lib/vehicles/purchase-quote";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import styles from "./VehicleReservation.module.css";

export function VehicleReservation({
  detail,
  access,
  promo,
  promoBusy,
  financing,
}: {
  detail: PublicVehicleDetail;
  access: CustomerVehicleAccess;
  promo: PurchaseQuote | null;
  promoBusy: boolean;
  financing: ReactNode;
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
          {financing}
        </div>
      </div>
    );
  }
  if (access.status === 403) {
    return (
      <div className={styles.reservation}>
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
      financing={financing}
    />
  );
}

function CustomerReservation({
  detail,
  account,
  promo,
  disabled,
  financing,
}: {
  detail: PublicVehicleDetail;
  account: CustomerAccount;
  promo: PurchaseQuote | null;
  disabled: boolean;
  financing: ReactNode;
}) {
  const reservation = useVehicleReservation(detail, account);
  const { command } = reservation;
  const receipt = command.receipt;
  const price = promo?.price ?? detail.price;
  if (receipt) {
    return (
      <section className={styles.receipt} aria-label="Reservation confirmation" tabIndex={-1}>
        <h2>Reservation confirmed</h2>
        <dl>
          <div>
            <dt>Reference</dt>
            <dd>{receipt.number}</dd>
          </div>
          <div>
            <dt>Buyer</dt>
            <dd>{receipt.buyer.legalName}</dd>
          </div>
          <div>
            <dt>Reserved total</dt>
            <dd>{formatCurrency(receipt.price.total)}</dd>
          </div>
          <div>
            <dt>Simulated deposit</dt>
            <dd>{formatCurrency(receipt.deposit)}</dd>
          </div>
          <div>
            <dt>Created</dt>
            <dd>{formatMalaysianDateTime(receipt.createdAt)}</dd>
          </div>
          <div>
            <dt>Hold expires</dt>
            <dd>{formatMalaysianDateTime(receipt.expiresAt)}</dd>
          </div>
          <div>
            <dt>Accepted promo</dt>
            <dd>{receipt.promoCode ?? "None"}</dd>
          </div>
        </dl>
        <p>
          Keep this reference. Confirmation email delivery may take a moment. The deposit is
          included in your cash contribution.
        </p>
        <Link href="/vehicles" prefetch={false}>
          Back to vehicles
        </Link>
        {financing}
      </section>
    );
  }
  return (
    <div className={styles.reservation}>
      <div className={styles.actions}>
        <Button
          disabled={disabled || reservation.locked}
          onClick={() => reservation.open(promo?.promoId ?? null)}
        >
          Reserve this car
        </Button>
        {financing}
      </div>
      <ConfirmationDialog
        open={Boolean(reservation.confirmation)}
        title="Reserve this car?"
        confirmLabel={
          command.uncertain ? "Reconcile reservation" : "Confirm RM500 simulated deposit"
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
          This portfolio simulation records a refundable RM500 deposit and holds the car for 48
          hours. No real payment is taken. The deposit counts toward your cash contribution.
        </p>
        <p>
          The backend confirms availability and current terms. Your receipt shows the reserved total
          and exact expiry. Cancellation or expiry records one full simulated refund.
        </p>
      </ConfirmationDialog>
    </div>
  );
}
