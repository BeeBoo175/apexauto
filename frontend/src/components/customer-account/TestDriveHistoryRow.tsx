import { Button } from "@/components/ui/Button";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { testDriveStatusLabels, type TestDriveHistoryItem } from "@/lib/test-drives/account";
import styles from "./ReservationHistory.module.css";
import account from "./Account.module.css";

export function TestDriveHistoryRow({
  item,
  disabled,
  onCancel,
}: {
  item: TestDriveHistoryItem;
  disabled: boolean;
  onCancel: () => void;
}) {
  return (
    <li>
      <details className={styles.historyRow}>
        <summary className={styles.textSummary}>
          <span className={styles.rowMain}>
            <strong>
              {item.manufactureYear} {item.make} {item.model} {item.trim}
            </strong>
            <span className={styles.help}>
              {formatMalaysianDateTime(item.startsAt)} · {item.branchName}
            </span>
          </span>
          <span className={styles.rowState}>{testDriveStatusLabels[item.status]}</span>
          <span className={styles.chevron} aria-hidden="true" />
        </summary>
        <div className={styles.historyTerms}>
          <dl className={account.details}>
            <div>
              <dt>Reference</dt>
              <dd className={account.reference}>{item.id}</dd>
            </div>
            {item.startedAt && (
              <div>
                <dt>Started</dt>
                <dd>{formatMalaysianDateTime(item.startedAt)}</dd>
              </div>
            )}
            {item.endedAt && (
              <div>
                <dt>Ended</dt>
                <dd>{formatMalaysianDateTime(item.endedAt)}</dd>
              </div>
            )}
            {item.cancelledAt && (
              <div>
                <dt>Cancelled</dt>
                <dd>{formatMalaysianDateTime(item.cancelledAt)}</dd>
              </div>
            )}
            {item.cancellationReason && (
              <div>
                <dt>Reason</dt>
                <dd>{item.cancellationReason}</dd>
              </div>
            )}
            {item.outcomeReason && (
              <div>
                <dt>Outcome</dt>
                <dd>{item.outcomeReason}</dd>
              </div>
            )}
          </dl>
          {item.status === "Booked" && (
            <Button variant="secondary" disabled={disabled} onClick={onCancel}>
              Request cancellation
            </Button>
          )}
        </div>
      </details>
    </li>
  );
}
