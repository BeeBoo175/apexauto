"use client";

import { useEffect, useRef } from "react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import type { useSalesQueue } from "@/hooks/use-sales-queue";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { claimAge, type SalesQueueItem, type SalesView } from "@/lib/sales/sales";
import { HoldWheel } from "./HoldWheel";
import styles from "@/components/inspection/TechnicianWorkspace.module.css";

const emptyMessages: Record<SalesView, string> = {
  Handling: "You are not handling any deals. Claim a hold to start one.",
  Waiting: "No deals are waiting for a manager's approval.",
  ToClaim: "No holds are waiting to be claimed.",
};

// One tab of the sales queue: the same list the technician workspace uses, with a wheel for open holds.
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
  view: SalesView;
  title: string;
  state: ReturnType<typeof useSalesQueue>;
  now: number;
  onOpen: (item: SalesQueueItem) => void;
  selectedId?: string;
  pendingId?: string;
  selectionLocked?: boolean;
}) {
  const continuation = useRef<HTMLDivElement | null>(null);
  const { snapshot, loadMore, busy, failed } = state;
  const claim = view === "ToClaim";
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
        {snapshot?.items.map((item) => (
          <li key={item.reservationId}>
            <Button
              variant="ghost"
              size="row"
              className={styles.job}
              onClick={() => onOpen(item)}
              aria-pressed={!claim && item.dealId === activeId}
              aria-busy={(!claim && item.dealId === pendingId) || undefined}
              disabled={selectionLocked && (claim || item.dealId !== selectedId)}
              aria-label={`${claim ? "Claim hold" : "Open deal"} · ${item.registration}`}
            >
              <span className={styles.jobHeading}>
                <span className={styles.identity}>
                  <strong>{item.registration}</strong>
                  <span>{item.vehicle}</span>
                  <span>{item.customer}</span>
                </span>
              </span>
              <span className={styles.jobState}>
                {!claim && item.nextStep && (
                  <Badge variant={view === "Waiting" ? "warning" : "info"}>{item.nextStep}</Badge>
                )}
                {claim && (
                  <HoldWheel
                    expiresAtUtc={item.expiresAtUtc}
                    frozenRemainingSeconds={item.frozenRemainingSeconds}
                    now={now}
                  />
                )}
                {!claim && item.claimedAtUtc && (
                  <HoldWheel
                    expiresAtUtc={item.expiresAtUtc}
                    frozenRemainingSeconds={item.frozenRemainingSeconds}
                    now={now}
                  />
                )}
                <span className={styles.jobMeta}>
                  {claim
                    ? `Reserved ${formatMalaysianDateTime(item.holdCreatedAtUtc)}`
                    : `Claimed ${claimAge(item.claimedAtUtc, now)}`}
                  {item.balancePayable !== null && ` · ${formatCurrency(item.balancePayable)}`}
                </span>
              </span>
            </Button>
          </li>
        ))}
      </ul>
      {(failed || more) && (
        <div className={styles.continuation} ref={continuation}>
          {failed && <p>The queue could not be loaded. Confirmed holds remain shown.</p>}
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
