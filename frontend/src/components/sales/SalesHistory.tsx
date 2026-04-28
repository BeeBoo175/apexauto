"use client";

import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import { Button } from "@/components/ui/Button";
import { useStaffHistory } from "@/hooks/use-staff-history";
import { privateApiFetch } from "@/lib/api-browser";
import { formatMalaysianDateTime } from "@/lib/date-format";
import {
  appendHoldHistory,
  historyChange,
  historyTitle,
  parseHoldHistory,
} from "@/lib/sales/history";
import { holdsPath } from "@/lib/sales/sales";
import styles from "./Sales.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

// The retained receipts of this hold and its deal: read-only, paged, newest first, as in the workshop history.
export function SalesHistory({ reservationId }: { reservationId: string }) {
  const { history, busy, failed, load, retry } = useStaffHistory(
    null,
    {
      path: `${holdsPath(reservationId)}/history`,
      parse: parseHoldHistory,
      matches: () => true,
      append: appendHoldHistory,
      read: privateApiFetch,
    },
    "The history could not be loaded. Confirmed receipts remain shown.",
  );
  return (
    <section className={styles.history} aria-label="Hold and deal history" aria-busy={busy}>
      <h3 className={workbench.sectionTitle}>History</h3>
      {!history && !busy && (
        <div className={workbench.protocol}>
          <p>{failed ? "The history could not be loaded." : "No history yet."}</p>
          <Button size="compact" variant="secondary" onClick={() => void retry()}>
            Retry history
          </Button>
        </div>
      )}
      {history && history.items.length === 0 && <p>No receipts on this page.</p>}
      {history?.items.map((item) => (
        <div className={styles.historyRow} key={item.id}>
          <strong>{historyTitle(item)}</strong>
          <span>
            {item.actor} · {formatMalaysianDateTime(item.recordedAtUtc)}
          </span>
          {item.reason && <span>{item.reason}</span>}
          {item.changes.length > 0 && (
            <ul>
              {item.changes.map((change) => (
                <li key={change.field}>{historyChange(change)}</li>
              ))}
            </ul>
          )}
          <span>Receipt {item.id}</span>
        </div>
      ))}
      {history && (
        <DataTablePagination
          page={{ ...history, total: history.totalCount }}
          count={history.items.length}
          disabled={busy}
          onPage={(page, size) => void load(page, size)}
        />
      )}
    </section>
  );
}
