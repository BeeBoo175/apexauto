import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { formatMalaysianDate } from "@/lib/date-format";
import {
  reservationVehicleName,
  type AccountReservation,
} from "@/lib/vehicles/account-reservations";
import { ReservationThumbnail } from "./ReservationThumbnail";
import styles from "./ReservationHistory.module.css";
import reservations from "./Reservations.module.css";

// A paid car that has not been handed over yet. Payment alone does not make it ready, so no wording says it is.
export function AwaitingCollectionRow({ item }: { item: AccountReservation }) {
  return (
    <li>
      <details className={styles.historyRow}>
        <summary className={styles.statelessSummary}>
          <ReservationThumbnail reservationId={item.receipt.id} photoId={item.coverPhotoId} />
          <span className={styles.rowMain}>
            <strong>{reservationVehicleName(item)}</strong>
            <span className={styles.help}>
              {item.settledAt && `Paid ${formatMalaysianDate(item.settledAt)} · `}
              {item.branchName}
            </span>
          </span>
          <span className={styles.chevron} aria-hidden="true" />
        </summary>
        <div className={styles.historyTerms}>
          <ol className={reservations.steps} aria-label="Purchase progress">
            <li data-state="done">
              Reserved
              <span className={reservations.stepDate}>
                {formatMalaysianDate(item.receipt.createdAt)}
              </span>
            </li>
            <li data-state="done">
              Payment confirmed
              {item.settledAt && (
                <span className={reservations.stepDate}>{formatMalaysianDate(item.settledAt)}</span>
              )}
            </li>
            <li data-state="current">Collected</li>
          </ol>
          <p className={styles.help}>
            Collect your car at {item.branchName}. Reservation {item.receipt.number}.
          </p>
          <div className={reservations.collectionLinks}>
            <Link href={`/branches?branchId=${item.receipt.branchId}`}>
              Showroom contact details
            </Link>
            <Link href="/buying-guide#collect">What to bring</Link>
          </div>
          <Link
            href={`/account/sales/${item.receipt.id}/documents`}
            className={buttonClassName("primary", "md", styles.documents)}
          >
            View sale documents
          </Link>
        </div>
      </details>
    </li>
  );
}
