"use client";

import { useState } from "react";
import Link from "next/link";
import buttonStyles from "@/components/ui/Button.module.css";
import { Button } from "@/components/ui/Button";
import { useAccountReservations } from "@/hooks/use-account-reservations";
import type { ApiResult } from "@/lib/api";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import {
  customerReservationStates,
  reservationVehicleName,
  type AccountReservation,
  type AccountReservations as ReservationSnapshot,
} from "@/lib/vehicles/account-reservations";
import { ReservedCar } from "./ReservedCar";
import { ReservationTerms } from "./ReservationTerms";
import { CancelReservation } from "./CancelReservation";
import styles from "./Reservations.module.css";

export function AccountReservations({
  initial,
  page,
  actorId,
  ready,
}: {
  initial: ApiResult<ReservationSnapshot>;
  page: number;
  actorId: string;
  ready: boolean;
}) {
  const history = useAccountReservations(initial, page);
  const [cancelling, setCancelling] = useState<AccountReservation | null>(null);
  const { snapshot } = history;
  const featured = snapshot?.active ?? history.released;
  const disabled = !ready || history.busy || Boolean(cancelling);
  const records = snapshot?.items.filter((item) => !item.isActive) ?? [];
  const empty = snapshot?.total === 0 && !featured;

  return (
    <section id="reservations" aria-labelledby="reservations-title" className={styles.section}>
      {empty ? (
        <div className={styles.empty}>
          <div>
            <h2 id="reservations-title">Your next car starts here.</h2>
            <p>When you reserve a car, your booking and its details will be right here.</p>
            <Link
              href="/vehicles"
              className={`${buttonStyles.button} ${buttonStyles.primary} ${buttonStyles.md}`}
            >
              Browse cars
            </Link>
          </div>
          <div className={styles.emptyAside}>
            <h3>A little time to decide.</h3>
            <p>Reserve your chosen car for 48 hours with a refundable MYR 500 simulated deposit.</p>
          </div>
        </div>
      ) : (
        <div className={styles.heading}>
          <h2 id="reservations-title">Your reservations</h2>
          <Button variant="ghost" disabled={disabled} onClick={() => void history.load()}>
            {history.busy ? "Refreshing reservations" : "Refresh reservations"}
          </Button>
        </div>
      )}
      {history.failed && (
        <p className={styles.notice} role="status">
          Reservations could not be refreshed. Any last confirmed records remain below. Use Refresh
          reservations to try again.
        </p>
      )}
      {featured && (
        <ReservedCar
          key={featured.receipt.id}
          item={featured}
          disabled={disabled}
          onCancel={() => setCancelling(featured)}
        />
      )}
      {snapshot && !featured && !empty && (
        <div className={styles.noActive}>
          <h3>No active reservation</h3>
          <p>Find your next car and reserve it with a refundable simulated deposit.</p>
          <Link
            href="/vehicles"
            className={`${buttonStyles.button} ${buttonStyles.secondary} ${buttonStyles.md}`}
          >
            Browse cars
          </Link>
        </div>
      )}
      {snapshot && snapshot.total > 0 && (
        <div className={styles.history}>
          <h3>Reservation history</h3>
          <p className={styles.help}>
            {snapshot.total} reservations · Page {history.page} · Newest first
          </p>
          {records.length === 0 && (
            <p className={styles.help}>No past reservations on this page.</p>
          )}
          {records.map((item) => (
            <article key={item.receipt.id} className={styles.historyItem}>
              <div className={styles.historySummary}>
                <div>
                  <h4>{reservationVehicleName(item)}</h4>
                  <p className={styles.help}>
                    {formatMalaysianDateTime(item.receipt.createdAt)} · {item.branchName}
                  </p>
                </div>
                <div className={styles.historyState}>
                  <p>{customerReservationStates[item.release!.kind]}</p>
                  <p className={styles.refund}>
                    {formatCurrency(item.release!.refund)} simulated refund recorded
                  </p>
                </div>
              </div>
              <details className={styles.disclosure}>
                <summary>View reservation {item.receipt.id}</summary>
                <ReservationTerms item={item} />
              </details>
            </article>
          ))}
          <nav aria-label="Reservation history pages" className={styles.paging}>
            <Button
              variant="secondary"
              disabled={disabled || history.page <= 1}
              onClick={() => void history.load(history.page - 1)}
            >
              Previous reservations
            </Button>
            <Button
              variant="secondary"
              disabled={disabled || history.page * snapshot.pageSize >= snapshot.total}
              onClick={() => void history.load(history.page + 1)}
            >
              Next reservations
            </Button>
          </nav>
        </div>
      )}
      {cancelling && (
        <CancelReservation
          item={cancelling}
          actorId={actorId}
          onClose={() => setCancelling(null)}
          onConfirmed={history.cancelled}
          onChanged={() => void history.load()}
        />
      )}
    </section>
  );
}
