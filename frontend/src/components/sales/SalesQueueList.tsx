"use client";

import { CustomerAvatar } from "@/components/staff/CustomerAvatar";
import { EmptyNotice } from "@/components/ui/EmptyNotice";
import { StaffQueueFooter } from "@/components/staff/StaffQueueFooter";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import type { useSalesQueue } from "@/hooks/use-sales-queue";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { IconName } from "@/lib/icons";
import {
  claimAge,
  queueViewOf,
  queueViewTabs,
  saleReference,
  saleHasHoldTimer,
  type QueueView,
  type SaleSummary,
} from "@/lib/sales/contracts";
import { HoldTimer } from "@/components/staff/HoldTimer";
import { SaleStatus } from "./SaleStatus";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";
import sales from "./Sales.module.css";

const emptyMessages: Record<QueueView | "Search", { icon: IconName; title: string; text: string }> =
  {
    Handling: {
      icon: "car",
      title: "Nothing in progress",
      text: "Claim a hold from To claim to start one.",
    },
    Waiting: {
      icon: "clock",
      title: "Nothing waiting",
      text: "Sales sent for approval appear here.",
    },
    Settling: {
      icon: "file-text",
      title: "Nothing to settle",
      text: "Sales awaiting handover appear here.",
    },
    Completed: {
      icon: "check",
      title: "No delivered sales",
      text: "Sales you hand over appear here.",
    },
    ToClaim: {
      icon: "car",
      title: "No holds to claim",
      text: "New holds appear here to claim.",
    },
    Search: {
      icon: "search",
      title: "No matches",
      text: "No sale matches this search.",
    },
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
  onClearSearch,
  onGoToClaim,
}: {
  view: QueueView | "Search";
  title: string;
  state: ReturnType<typeof useSalesQueue>;
  now: number;
  onOpen: (item: SaleSummary) => void;
  selectedId?: string;
  pendingId?: string;
  selectionLocked?: boolean;
  // Clears the search in the queue's search box and shows the tabs again; only search results offer it.
  onClearSearch?: () => void;
  // Opens the To claim tab, which the empty Handling tab points to.
  onGoToClaim?: () => void;
}) {
  const { snapshot, busy, failed } = state;
  const activeId = pendingId || selectedId;
  const empty = emptyMessages[view];

  return (
    <section className={styles.queueList} aria-label={title} aria-busy={busy}>
      <div className={styles.list} key={`${snapshot?.page}:${snapshot?.pageSize}:${state.search}`}>
        {!snapshot && !failed && !busy && (
          <EmptyNotice
            icon="alert-triangle"
            tone="alert"
            title="Queue unavailable"
            action={{ label: "Retry queue", disabled: busy, onClick: () => void state.retry() }}
          >
            Your queues are kept as they were.
          </EmptyNotice>
        )}
        {snapshot?.items.length === 0 && (
          <EmptyNotice
            icon={empty.icon}
            title={empty.title}
            link={
              view === "Handling" && onGoToClaim
                ? { text: "To claim", onClick: onGoToClaim }
                : undefined
            }
            action={
              onClearSearch && view === "Search"
                ? { label: "Clear search", onClick: onClearSearch }
                : undefined
            }
          >
            {empty.text}
          </EmptyNotice>
        )}
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
                      <span>
                        <CustomerAvatar id={item.customerId} name={item.customer} nested />
                      </span>
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
                      <HoldTimer dueAt={item.dueAt} pausedSeconds={item.pausedSeconds} now={now} />
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
      </div>
      <StaffQueueFooter
        page={snapshot ?? null}
        count={snapshot?.items.length ?? 0}
        busy={busy}
        failed={failed}
        onPage={(page, pageSize) => void state.changePage(page, pageSize)}
        onRetry={() => void state.retry()}
      />
    </section>
  );
}
