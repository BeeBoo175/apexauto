import Image from "next/image";
import Link from "next/link";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import {
  customerReservationStates,
  reservationVehicleName,
  type AccountReservation,
} from "@/lib/vehicles/account-reservations";
import { ReservationTerms } from "./ReservationTerms";
import styles from "./ReservationHistory.module.css";

export function ReservationHistoryRow({ item }: { item: AccountReservation }) {
  const { release } = item;
  return (
    <li>
      <details className={styles.historyRow}>
        <summary>
          <span className={styles.thumb}>
            {item.coverPhotoId && (
              <Image
                src={`/api/account/reservations/${item.receipt.id}/photos/${item.coverPhotoId}`}
                alt=""
                width={96}
                height={64}
                unoptimized
                onError={(event) => {
                  event.currentTarget.hidden = true;
                }}
              />
            )}
          </span>
          <span className={styles.rowMain}>
            <strong>{reservationVehicleName(item)}</strong>
            <span className={styles.help}>
              {formatMalaysianDateTime(item.receipt.createdAt)} · {item.branchName}
            </span>
          </span>
          <span className={styles.rowState}>
            {release ? customerReservationStates[release.kind] : "Sale confirmed"}
          </span>
          {release && (
            <span className={styles.rowAmount}>
              {formatCurrency(release.refund)}
              <span className={styles.help}>refunded</span>
            </span>
          )}
          <span className={styles.chevron} aria-hidden="true" />
        </summary>
        <div className={styles.historyTerms}>
          {!release && (
            <Link href={`/account/sales/${item.receipt.id}/documents`}>View sale documents</Link>
          )}
          <ReservationTerms item={item} />
        </div>
      </details>
    </li>
  );
}
