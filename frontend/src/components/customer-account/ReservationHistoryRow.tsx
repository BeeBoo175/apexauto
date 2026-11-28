import Link from "next/link";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDate, formatMalaysianDateTime } from "@/lib/date-format";
import {
  purchaseStateLabel,
  reservationVehicleName,
  type AccountReservation,
} from "@/lib/vehicles/account-reservations";
import { ReservationTerms } from "./ReservationTerms";
import { ReservationThumbnail } from "./ReservationThumbnail";
import styles from "./ReservationHistory.module.css";

export function ReservationHistoryRow({
  item,
  datedByCollection = false,
}: {
  item: AccountReservation;
  datedByCollection?: boolean;
}) {
  const { release } = item;
  const date = (datedByCollection && item.deliveredAt) || item.receipt.createdAt;
  return (
    <li>
      <details className={styles.historyRow}>
        <summary>
          <ReservationThumbnail reservationId={item.receipt.id} photoId={item.coverPhotoId} />
          <span className={styles.rowMain}>
            <strong>{reservationVehicleName(item)}</strong>
            <span className={styles.help}>
              {formatMalaysianDateTime(date)} · {item.branchName}
            </span>
          </span>
          <span className={styles.rowState}>{purchaseStateLabel(item)}</span>
          {release && (
            <span className={styles.rowAmount}>
              {formatCurrency(release.refund)}
              <span className={styles.help}>refunded</span>
            </span>
          )}
          <span className={styles.chevron} aria-hidden="true" />
        </summary>
        <div className={styles.historyTerms}>
          {item.deliveredAt && <p>Collected on {formatMalaysianDate(item.deliveredAt)}.</p>}
          {!release && (
            <Link href={`/account/sales/${item.receipt.id}/documents`}>View sale documents</Link>
          )}
          <ReservationTerms item={item} />
        </div>
      </details>
    </li>
  );
}
