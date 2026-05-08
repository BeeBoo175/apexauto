"use client";

import { useEffect, useRef } from "react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import type { useSalesQueue } from "@/hooks/use-sales-queue";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import {
  claimAge,
  queueViewOf,
  queueViewTabs,
  saleReference,
  type QueueView,
  type SaleSummary,
} from "@/lib/sales/contracts";
import { HoldWheel } from "./HoldWheel";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";
import sales from "./Sales.module.css";

const emptyMessages: Record<QueueView | "Search", string> = {
  Handling: "You are not handling any sales. Claim a hold to start one.",
  Waiting: "No sales are waiting for a manager's approval.",
  Approved: "None of your sales are approved yet.",
  ToClaim: "No holds are waiting to be claimed.",
  Search: "No sales match this search.",
};

// One tab of the agent's queue, or search results across all tabs: the technician workspace's list, with the
// hold's time on each row and, for search results, the tab the sale sits in.
export function SalesQueueList({
  view,
  title,
  state,
  now,
  onOpen,
  selectedId = "",
  pendingId = "",
  selectionLocked = false,
}: {
  view: QueueView | "Search";
  title: string;
  state: ReturnType<typeof useSalesQueue>;
  now: number;
  onOpen: (item: SaleSummary) => void;
  selectedId?: string;
  pendingId?: string;
  selectionLocked?: boolean;
}) {
  const continuation = useRef<HTMLDivElement | null>(null);
  const { snapshot, loadMore, busy, failed } = state;
  const activeId = pendingId || selectedId;
  const more = Boolean(snapshot && snapshot.items.length < snapshot.total);
  useEffect(() => {
    const target = continuation.current;
    if (!target || busy || failed || !more || typeof IntersectionObserver === "undefined") {
      return;
    }
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((entry) => entry.isIntersecting)) {
        void loadMore();
      }
    });
    observer.observe(target);
    return () => observer.disconnect();
  }, [busy, failed, more, loadMore]);

  return (
    <section className={styles.list} aria-label={title} aria-busy={busy}>
      {!snapshot && !failed && (
        <div className={styles.empty}>
          <p>This queue is unavailable.</p>
          <Button
            size="compact"
            variant="secondary"
            disabled={busy}
            onClick={() => void state.retry()}
          >
            Retry queue
          </Button>
        </div>
      )}
      {snapshot?.items.length === 0 && <p className={styles.empty}>{emptyMessages[view]}</p>}
      <ul className={styles.jobs}>
        {snapshot?.items.map((item) => {
          const open = item.status === "Open";
          const tab = queueViewOf[item.status];
          return (
            <li key={item.id}>
              <Button
                variant="ghost"
                size="row"
                className={styles.job}
                onClick={() => onOpen(item)}
                aria-pressed={!open && item.id === activeId}
                aria-busy={(!open && item.id === pendingId) || undefined}
                disabled={selectionLocked && (open || item.id !== selectedId)}
                aria-label={`${open ? "Claim hold" : "Open sale"} · ${item.registration}`}
              >
                <span className={styles.jobHeading}>
                  <span className={styles.identity}>
                    <strong>{item.registration}</strong>
                    <span>
                      {item.manufactureYear} {item.make} {item.model}
                    </span>
                    <span>{item.customer}</span>
                  </span>
                  {!open && (
                    <span className={sales.queueBalance}>
                      <span className="sr-only">Balance payable </span>
                      {formatCurrency(item.balancePayable)}
                    </span>
                  )}
                </span>
                <span className={styles.jobState}>
                  {view === "Search" && tab && <Badge variant="info">{queueViewTabs[tab]}</Badge>}
                  {(open || item.pausedSeconds !== null) && (
                    <HoldWheel dueAt={item.dueAt} pausedSeconds={item.pausedSeconds} now={now} />
                  )}
                  <span className={styles.jobMeta}>
                    {saleReference(item)}
                    {open
                      ? ` · Expires ${formatMalaysianDateTime(item.dueAt ?? "")}`
                      : ` · Claimed ${claimAge(item.claimedAt, now)}`}
                  </span>
                </span>
              </Button>
            </li>
          );
        })}
      </ul>
      {(failed || more) && (
        <div className={styles.continuation} ref={continuation}>
          {failed && <p>The queue could not be loaded. Confirmed sales remain shown.</p>}
          <Button
            size="compact"
            variant="secondary"
            disabled={busy}
            onClick={() => void (failed ? state.retry() : loadMore())}
          >
            {failed ? "Retry queue" : "Load more"}
          </Button>
          <span role="status" aria-live="polite">
            {busy ? "Loading…" : `${snapshot?.items.length ?? 0} loaded`}
          </span>
        </div>
      )}
    </section>
  );
}
