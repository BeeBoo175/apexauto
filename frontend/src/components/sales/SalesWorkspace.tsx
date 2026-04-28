"use client";

import { useContext, useState } from "react";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { useToast } from "@/components/ui/Toast";
import { useSalesQueue } from "@/hooks/use-sales-queue";
import { useStaffSelection } from "@/hooks/use-staff-selection";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { dealPath, parseDeal, type Deal } from "@/lib/deals/deals";
import {
  salesViewLabels,
  salesViewQueries,
  salesViewTabs,
  salesViews,
  type SalesQueue,
  type SalesQueueItem,
  type SalesSection,
  type SalesView,
} from "@/lib/sales/sales";
import { SalesClaim } from "./SalesClaim";
import { SalesContent } from "./SalesContent";
import { SalesQueueList } from "./SalesQueueList";
import styles from "@/components/inspection/TechnicianWorkspace.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

const countKeys = { Handling: "handling", Waiting: "waiting", ToClaim: "toClaim" } as const;

export interface SalesWorkspaceProps {
  actor: CurrentActor;
  queues: Record<SalesView, ApiResult<SalesQueue>>;
  initialView: SalesView;
  selectedDealId: string;
  selectedDeal: ApiResult<Deal> | null;
  selectedSection: SalesSection;
}

// The agent's workspace, laid out as the technician's: queues with counts on the left, the selected deal on the right.
export function SalesWorkspace({
  actor,
  queues,
  initialView,
  selectedDealId,
  selectedDeal,
  selectedSection,
}: SalesWorkspaceProps) {
  const animateEntry = useContext(StaffPageEntranceContext);
  const toast = useToast();
  const [view, setView] = useState(initialView);
  const [section, setSection] = useState(selectedSection);
  const [queueExpanded, setQueueExpanded] = useState(!selectedDealId);
  // Hold timers are read against this moment, which moves forward each time the queues are refreshed.
  const [now, setNow] = useState(() => Date.now());
  const handling = useSalesQueue("Handling", queues.Handling);
  const waiting = useSalesQueue("Waiting", queues.Waiting);
  const claimable = useSalesQueue("ToClaim", queues.ToClaim);
  const worklists = { Handling: handling, Waiting: waiting, ToClaim: claimable };
  const selection = useStaffSelection<Deal>(selectedDealId, selectedDeal, {
    parameter: "deal",
    path: dealPath,
    parse: parseDeal,
    matches: (value, id) => value.id === id,
    read: privateApiFetch,
    commitAfterRead: true,
  });
  const busy = handling.busy || waiting.busy || claimable.busy;
  const interactive = handling.ready && waiting.ready && claimable.ready;
  const chosen = Boolean(selection.id || selection.pendingId);

  async function refresh() {
    setNow(Date.now());
    const results = await Promise.all(salesViews.map((item) => worklists[item].refresh(false)));
    if (results.some((result) => result === false)) {
      toast.error(
        "The sales queues could not all be refreshed. The last confirmed queues remain shown.",
      );
    }
  }

  function showView(value: SalesView) {
    setView(value);
    const url = new URL(window.location.href);
    url.searchParams.set("view", salesViewQueries[value]);
    window.history.replaceState(null, "", url);
  }

  function changeSection(next: SalesSection) {
    setSection(next);
    const url = new URL(window.location.href);
    url.searchParams.set("section", next);
    window.history.replaceState(null, "", url);
  }

  function open(item: SalesQueueItem) {
    if (item.dealId) {
      setQueueExpanded(false);
      void selection.choose(item.dealId, (url) => url.searchParams.set("section", section));
    }
  }

  return (
    <div className={styles.workspace}>
      <LoadingProgress label="Loading sales queues" active={busy || selection.busy} />
      <StaffCommandBar
        title="Sales"
        actions={
          <RefreshButton
            label="Refresh sales"
            busy={busy}
            disabled={!interactive || selection.busy}
            onClick={() => void refresh()}
          />
        }
      />
      <div
        className={`${styles.columns} ${animateEntry ? workbench.reveal : ""}`}
        data-selected={chosen}
        inert={!interactive}
      >
        <aside className={styles.queue} aria-label="Sales queues" data-expanded={queueExpanded}>
          {chosen && (
            <div className={styles.mobileQueueBar}>
              <strong>{salesViewLabels[view]}</strong>
              <Button
                size="compact"
                variant="secondary"
                aria-expanded={queueExpanded}
                aria-controls="sales-queue-content"
                onClick={() => setQueueExpanded(!queueExpanded)}
              >
                {queueExpanded ? "Hide queue" : "Show queue"}
              </Button>
            </div>
          )}
          <div className={styles.queueBody} id="sales-queue-content">
            <nav className={styles.queueNavigation} aria-label="Sales queue tabs">
              {salesViews.map((item) => (
                <Button
                  key={item}
                  size="compact"
                  variant="ghost"
                  className={styles.queueView}
                  aria-pressed={view === item}
                  title={salesViewLabels[item]}
                  onClick={() => showView(item)}
                >
                  <span>{salesViewTabs[item]}</span>
                  {worklists[item].snapshot && (
                    <Badge variant="count" data-selected={view === item}>
                      {worklists[item].snapshot.counts[countKeys[item]]}
                      <span className="sr-only"> holds</span>
                    </Badge>
                  )}
                </Button>
              ))}
            </nav>
            {view === "ToClaim" ? (
              <SalesClaim
                state={claimable}
                now={now}
                selectionLocked={false}
                onConflict={() => void refresh()}
                onClaimed={(deal) => {
                  setQueueExpanded(false);
                  showView("Handling");
                  changeSection("customer");
                  void refresh();
                  void selection.choose(deal.id, (url) =>
                    url.searchParams.set("section", "customer"),
                  );
                }}
              />
            ) : (
              <SalesQueueList
                key={view}
                view={view}
                title={salesViewLabels[view]}
                state={worklists[view]}
                now={now}
                onOpen={open}
                selectedId={selection.id}
                pendingId={selection.pendingId}
              />
            )}
          </div>
        </aside>
        <SalesContent
          actor={actor}
          selection={selection}
          section={section}
          now={now}
          onSection={changeSection}
          onChanged={async () => {
            await selection.choose(selection.id);
            await refresh();
          }}
          onReleased={() => {
            selection.close();
            showView("ToClaim");
            setQueueExpanded(true);
            void refresh();
          }}
        />
      </div>
    </div>
  );
}
