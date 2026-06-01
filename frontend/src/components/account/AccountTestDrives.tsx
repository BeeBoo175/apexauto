"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { useAccountTestDrives } from "@/hooks/use-account-test-drives";
import type { ApiResult } from "@/lib/api";
import type { HistoryFilters } from "@/lib/intake/list-query";
import { formatMalaysianDateTime } from "@/lib/date-format";
import {
  testDriveStatusLabels,
  type TestDriveHistory,
  type TestDriveHistoryItem,
} from "@/lib/test-drives/account";
import { CancelTestDrive } from "./CancelTestDrive";
import { HistoryFilterForm } from "./HistoryFilterForm";
import { EmptyState } from "./EmptyState";
import styles from "./Account.module.css";

export function AccountTestDrives({
  initial,
  page,
  filters,
  actorId,
  ready,
}: {
  initial: ApiResult<TestDriveHistory>;
  page: number;
  filters: HistoryFilters;
  actorId: string;
  ready: boolean;
}) {
  const history = useAccountTestDrives(initial, page, filters, actorId);
  const [cancelling, setCancelling] = useState<TestDriveHistoryItem | null>(null);
  const { snapshot } = history;
  const disabled = !ready || history.busy || Boolean(cancelling);
  const empty = snapshot?.total === 0 && !history.filters.from && !history.filters.to;
  return (
    <section className={styles.history} aria-labelledby="test-drive-history-title">
      <div className={styles.heading}>
        <h2 id="test-drive-history-title">Your test drives</h2>
        <Button variant="secondary" disabled={disabled} onClick={() => void history.load()}>
          {history.busy ? "Refreshing test drives" : "Refresh test drives"}
        </Button>
      </div>
      <p className={styles.help}>
        Your booked test drives, newest first. All times are in Malaysia time.
      </p>
      {!empty && (
        <HistoryFilterForm
          draft={history.draft}
          disabled={disabled}
          busy={history.busy}
          onDraft={history.setDraft}
          onApply={() => void history.load(1, history.draft)}
        />
      )}
      {empty && (
        <EmptyState
          title="No test drives yet"
          description="Choose a car and book a 30-minute test drive at its branch."
          href="/vehicles"
          action="Browse cars"
          prefetch={false}
        />
      )}
      {snapshot && !empty && (
        <>
          <p className={styles.help}>
            {snapshot.total} test drives · Page {history.page}
          </p>
          {snapshot.items.length === 0 && <p>No test drives match these dates.</p>}
          <div className={styles.records}>
            {snapshot.items.map((item) => (
              <article key={item.id} className={styles.appointment}>
                <div className={styles.heading}>
                  <h3>
                    {item.manufactureYear} {item.make} {item.model} {item.trim}
                  </h3>
                  <span className={styles.state}>{testDriveStatusLabels[item.status]}</span>
                </div>
                <dl className={styles.details}>
                  <div>
                    <dt>Branch</dt>
                    <dd>{item.branchName}</dd>
                  </div>
                  <div>
                    <dt>Booked time</dt>
                    <dd>{formatMalaysianDateTime(item.startsAt)}</dd>
                  </div>
                  <div>
                    <dt>Reference</dt>
                    <dd className={styles.reference}>{item.id}</dd>
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
                  <Button
                    variant="secondary"
                    disabled={disabled}
                    onClick={() => setCancelling(item)}
                  >
                    Request cancellation
                  </Button>
                )}
              </article>
            ))}
          </div>
          <nav className={styles.paging} aria-label="Test-drive history pages">
            <Button
              variant="secondary"
              disabled={disabled || history.page <= 1}
              onClick={() => void history.load(history.page - 1)}
            >
              Previous test drives
            </Button>
            <Button
              variant="secondary"
              disabled={disabled || history.page * snapshot.pageSize >= snapshot.total}
              onClick={() => void history.load(history.page + 1)}
            >
              Next test drives
            </Button>
          </nav>
        </>
      )}
      {cancelling && (
        <CancelTestDrive
          key={cancelling.id}
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
