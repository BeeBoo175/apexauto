"use client";

import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import type { useAccountCollectionHistory } from "@/hooks/use-account-collection-history";
import { HistoryFilterForm } from "./HistoryFilterForm";
import { ReservationHistoryRow } from "./ReservationHistoryRow";
import styles from "./Reservations.module.css";
import historyStyles from "./ReservationHistory.module.css";

// Cars already handed over, in the same shape as the reservation history, dated by their collection.
export function CollectionHistory({
  history,
  snapshot,
  ready,
}: {
  history: ReturnType<typeof useAccountCollectionHistory>;
  snapshot: NonNullable<ReturnType<typeof useAccountCollectionHistory>["snapshot"]>;
  ready: boolean;
}) {
  const disabled = !ready || history.busy;
  const filtered = Boolean(
    history.filters.from || history.filters.to || history.filters.search.trim(),
  );
  const pages = Math.max(1, Math.ceil(snapshot.total / snapshot.pageSize));
  const first = snapshot.items.length ? (history.page - 1) * snapshot.pageSize + 1 : 0;
  const last = first ? first + snapshot.items.length - 1 : 0;

  return (
    <section className={historyStyles.history} aria-labelledby="collection-history-title">
      <div className={historyStyles.historyHead}>
        <h3 id="collection-history-title">Collection history</h3>
        {snapshot.collectedTotal > 0 && (
          <p className={styles.help}>
            {snapshot.total} {snapshot.total === 1 ? "car" : "cars"} · Page {history.page} · Latest
            collection first
          </p>
        )}
      </div>
      {snapshot.collectedTotal === 0 && (
        <p className={styles.help}>Cars you have collected appear here.</p>
      )}
      {snapshot.collectedTotal > 0 && (
        <>
          <HistoryFilterForm
            draft={history.draft}
            disabled={disabled}
            busy={history.busy}
            searchLabel="Search collection history"
            searchPlaceholder="Search reservation number, vehicle or branch"
            onDraft={history.setDraft}
            onApply={() => void history.load(1, history.draft)}
            onClearSearch={() => {
              const next = { ...history.draft, search: "" };
              history.setDraft(next);
              void history.load(1, next);
            }}
          />
          {snapshot.items.length === 0 && (
            <div className={historyStyles.noMatches}>
              <p className={styles.help}>No collected cars match your search.</p>
              {filtered && (
                <Button
                  variant="ghost"
                  size="sm"
                  disabled={disabled}
                  onClick={() => {
                    const cleared = { from: "", to: "", search: "" };
                    history.setDraft(cleared);
                    void history.load(1, cleared);
                  }}
                >
                  Clear filters
                </Button>
              )}
            </div>
          )}
          <ul className={historyStyles.historyList}>
            {snapshot.items.map((item) => (
              <ReservationHistoryRow key={item.receipt.id} item={item} datedByCollection />
            ))}
          </ul>
          {snapshot.total > 0 && (
            <nav aria-label="Collection history pages" className={historyStyles.pager}>
              <span className={historyStyles.pagerMeta} aria-live="polite">
                {first}–{last} of {snapshot.total}
              </span>
              <div className={historyStyles.pagerControls}>
                <Button
                  variant="ghost"
                  size="icon"
                  disabled={disabled || history.page <= 1}
                  onClick={() => void history.load(history.page - 1)}
                  aria-label="Previous collected cars"
                  title="Previous page"
                >
                  <Icon name="previous" />
                </Button>
                <span>
                  Page {history.page} of {pages}
                </span>
                <Button
                  variant="ghost"
                  size="icon"
                  disabled={disabled || history.page >= pages}
                  onClick={() => void history.load(history.page + 1)}
                  aria-label="Next collected cars"
                  title="Next page"
                >
                  <Icon name="next" />
                </Button>
              </div>
            </nav>
          )}
        </>
      )}
    </section>
  );
}
