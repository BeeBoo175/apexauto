"use client";

import { useState } from "react";
import Image from "next/image";
import { Button } from "@/components/ui/Button";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import {
  customerReservationStates,
  reservationVehicleName,
  type AccountReservation,
} from "@/lib/vehicles/account-reservations";
import { ReservationTerms } from "./ReservationTerms";
import styles from "./Reservations.module.css";

// What the customer reads first: how the hold ended, or whether our team is already handling it.
function stateLabel(item: AccountReservation) {
  if (item.release) {
    return customerReservationStates[item.release.kind];
  }
  return item.isHandled ? "Being handled by our sales team" : "Reserved for you";
}

export function ReservedCar({
  item,
  disabled,
  onCancel,
}: {
  item: AccountReservation;
  disabled: boolean;
  onCancel: () => void;
}) {
  const [failedPhoto, setFailedPhoto] = useState(false);
  const name = reservationVehicleName(item);
  return (
    <article aria-label="Your reserved car" className={styles.reservedCar}>
      <div className={styles.hero}>
        <div className={styles.photo}>
          {item.coverPhotoId && !failedPhoto ? (
            <Image
              src={`/api/account/reservations/${item.receipt.id}/photos/${item.coverPhotoId}`}
              alt={name}
              width={960}
              height={640}
              unoptimized
              loading="eager"
              onError={() => setFailedPhoto(true)}
            />
          ) : (
            <p>Vehicle photography unavailable</p>
          )}
        </div>
        <div className={styles.summary}>
          <p className={styles.state}>{stateLabel(item)}</p>
          <h3>
            {item.manufactureYear} {item.make} {item.model}
          </h3>
          <p className={styles.location}>
            {item.trim} · {item.branchName}
          </p>
          <div className={styles.price}>
            <span>Your reserved total</span>
            <strong>{formatCurrency(item.receipt.price.total)}</strong>
          </div>
          {item.isActive && item.isHandled && (
            <p className={styles.help}>Expect a call or email from us to agree your final terms.</p>
          )}
          {item.isActive && !item.isHandled && (
            <div className={styles.deadline}>
              <span>Held until (Malaysia)</span>
              <strong>{formatMalaysianDateTime(item.dueAt ?? item.receipt.expiresAt)}</strong>
            </div>
          )}
          {item.release && (
            <p className={styles.refund}>
              {formatCurrency(item.release.refund)} simulated refund recorded
            </p>
          )}
          <p className={styles.help}>
            {formatCurrency(item.receipt.deposit)} simulated deposit · Fully refundable
          </p>
        </div>
      </div>
      {item.isActive && (
        <ol className={styles.steps} aria-label="Reservation progress">
          <li data-state="done">Reserved</li>
          <li data-state={item.isHandled ? "current" : "next"}>With our sales team</li>
          <li data-state="next">Final terms agreed</li>
        </ol>
      )}
      <div className={styles.reservationActions}>
        <p className={styles.reference}>Reservation {item.receipt.number}</p>
        {/* Once our team handles the sale, changes are agreed with them rather than cancelled online. */}
        {item.isActive && !item.isHandled && (
          <Button variant="ghost" disabled={disabled} onClick={onCancel}>
            Cancel reservation
          </Button>
        )}
      </div>
      <details className={styles.disclosure}>
        <summary>View reservation details</summary>
        <ReservationTerms item={item} />
      </details>
    </article>
  );
}
