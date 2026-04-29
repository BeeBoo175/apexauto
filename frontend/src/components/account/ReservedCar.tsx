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
          <p className={styles.state}>
            {item.release ? customerReservationStates[item.release.kind] : "Reserved for you"}
          </p>
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
          {item.isActive ? (
            <div className={styles.deadline}>
              <span>Held until (Malaysia)</span>
              <strong>{formatMalaysianDateTime(item.receipt.expiresAt)}</strong>
            </div>
          ) : (
            <p className={styles.refund}>
              {formatCurrency(item.release!.refund)} simulated refund recorded
            </p>
          )}
          <p className={styles.help}>
            {formatCurrency(item.receipt.deposit)} simulated deposit · Fully refundable
          </p>
        </div>
      </div>
      <div className={styles.reservationActions}>
        <p className={styles.reference}>Reservation {item.receipt.number}</p>
        {item.isActive && (
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
