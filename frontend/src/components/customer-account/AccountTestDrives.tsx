"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { useAccountTestDrives } from "@/hooks/use-account-test-drives";
import { TestDriveHistoryRow } from "./TestDriveHistoryRow";
import { type TestDriveHistoryItem, type TestDriveHistoryFilters } from "@/lib/test-drives/account";
import { CancelTestDrive } from "./CancelTestDrive";
import { HistoryFilterForm } from "./HistoryFilterForm";
import { EmptyState } from "./EmptyState";
import { AccountLoadingShell } from "./AccountLoadingShell";
import styles from "./Account.module.css";
import historyStyles from "./ReservationHistory.module.css";

export function AccountTestDrives({
  page,
  filters,
  actorId,
  ready,
}: {
  page: number;
  filters: TestDriveHistoryFilters;
  actorId: string;
  ready: boolean;
}) {
  const history = useAccountTestDrives(page, filters, actorId);
  const [cancelling, setCancelling] = useState<TestDriveHistoryItem | null>(null);
  const { snapshot } = history;
  const disabled = !ready || history.busy || Boolean(cancelling);
  const filtered = Boolean(
    history.filters.from || history.filters.to || history.filters.search.trim(),
  );
  const empty = snapshot?.total === 0 && !filtered;
  return (
    <section
      className={`${styles.history} ${historyStyles.historyScope}`}
      aria-labelledby="test-drive-history-title"
    >
      <div className={styles.heading}>
        <div>
          <h2 id="test-drive-history-title">Your test drives</h2>
          <p className={styles.help}>
            Your appointments to drive a car, with their dates and status.
          </p>
        </div>
      </div>
      {!snapshot && history.busy && <AccountLoadingShell label="Loading test drives" />}
      {!snapshot && !history.busy && (
        <Button variant="secondary" disabled={disabled} onClick={() => void history.load()}>
          Try loading test drives again
        </Button>
      )}
      {!empty && (
        <HistoryFilterForm
          draft={history.draft}
          disabled={disabled}
          busy={history.busy}
          searchLabel="Search test drives"
          searchPlaceholder="Search booking reference, vehicle or branch"
          onClearSearch={() => {
            const next = { ...history.draft, search: "" };
            history.setDraft(next);
            void history.load(1, next);
          }}
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
          {snapshot.items.length === 0 && (
            <EmptyState
              title="No matching test drives"
              description="Try another search or date range, or browse cars to book a drive."
              href="/vehicles"
              action="Browse cars"
              prefetch={false}
            />
          )}
          {snapshot.items.length === 0 && filtered && (
            <Button
              variant="ghost"
              size="compact"
              disabled={disabled}
              onClick={() => {
                const next = { from: "", to: "", search: "" };
                history.setDraft(next);
                void history.load(1, next);
              }}
            >
              Clear filters
            </Button>
          )}
          <ul className={historyStyles.historyList}>
            {snapshot.items.map((item) => (
              <TestDriveHistoryRow
                key={item.id}
                item={item}
                disabled={disabled}
                onCancel={() => setCancelling(item)}
              />
            ))}
          </ul>
          <nav className={historyStyles.pager} aria-label="Test-drive history pages">
            <span className={historyStyles.pagerMeta} aria-live="polite">
              {snapshot.total} test drives
            </span>
            <div className={historyStyles.pagerControls}>
              <Button
                variant="ghost"
                size="icon"
                disabled={disabled || history.page <= 1}
                onClick={() => void history.load(history.page - 1)}
              >
                <span className="sr-only">Previous test drives</span>
                <Icon name="previous" />
              </Button>
              <span>
                Page {history.page} of {Math.max(1, Math.ceil(snapshot.total / snapshot.pageSize))}
              </span>
              <Button
                variant="ghost"
                size="icon"
                disabled={disabled || history.page * snapshot.pageSize >= snapshot.total}
                onClick={() => void history.load(history.page + 1)}
              >
                <span className="sr-only">Next test drives</span>
                <Icon name="next" />
              </Button>
            </div>
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
