"use client";

import { StaffQueueContinuation } from "@/components/staff/StaffQueueContinuation";
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
  saleHasHoldTimer,
  type QueueView,
  type SaleSummary,
} from "@/lib/sales/contracts";
import { HoldWheel } from "./HoldWheel";
import { SaleStatus } from "./SaleStatus";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";
import sales from "./Sales.module.css";

const emptyMessages: Record<QueueView | "Search", string> = {
  Handling: "You are not handling any sales. Claim a hold to start one.",
  Waiting: "No sales are waiting for a manager's approval.",
  Settling: "No sales are awaiting settlement or handover.",
  Completed: "You have no delivered sales.",
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
  const { snapshot, loadMore, busy, failed } = state;
  const activeId = pendingId || selectedId;
  const more = Boolean(snapshot && snapshot.items.length < snapshot.total);

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
                  {(item.status === "Approved" ||
                    item.status === "Settled" ||
                    item.status === "Delivered") && <SaleStatus status={item.status} />}
                  {saleHasHoldTimer(item.status) && (
                    <HoldWheel dueAt={item.dueAt} pausedSeconds={item.pausedSeconds} now={now} />
                  )}
                  <span className={styles.jobMeta}>
                    {saleReference(item)}
                    {item.status === "Delivered" &&
                      item.closedAt &&
                      ` · Delivered ${formatMalaysianDateTime(item.closedAt)}`}
                    {open && ` · Expires ${formatMalaysianDateTime(item.dueAt ?? "")}`}
                    {!open &&
                      item.status !== "Delivered" &&
                      ` · Claimed ${claimAge(item.claimedAt, now)}`}
                  </span>
                </span>
              </Button>
            </li>
          );
        })}
      </ul>
      <StaffQueueContinuation
        loaded={snapshot?.items.length ?? 0}
        total={snapshot?.total}
        more={more}
        busy={busy}
        failed={failed}
        onMore={() => void loadMore()}
        onRetry={() => void state.retry()}
      />
    </section>
  );
}
