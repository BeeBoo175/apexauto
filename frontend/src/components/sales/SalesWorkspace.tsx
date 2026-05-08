"use client";

import { useContext, useState } from "react";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { SearchField } from "@/components/ui/SearchField";
import { useToast } from "@/components/ui/Toast";
import { useSaleSelection } from "@/hooks/use-sale-selection";
import { useMinuteClock } from "@/hooks/use-minute-clock";
import { useSalesQueue } from "@/hooks/use-sales-queue";
import type { ApiResult } from "@/lib/api";
import {
  type Sale,
  queueCountOf,
  queueViewLabels,
  queueViewQueries,
  queueViewTabs,
  queueViews,
  type QueueView,
  type SaleSummary,
  type SalesQueue,
} from "@/lib/sales/contracts";
import type { SaleSection } from "@/lib/sales/sale-sections";
import { SalesClaim } from "./SalesClaim";
import { SalesContent } from "./SalesContent";
import { SalesQueueList } from "./SalesQueueList";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export interface SalesWorkspaceProps {
  // The opening tab is read on entry; the others are read the first time they are shown.
  queues: Partial<Record<QueueView, ApiResult<SalesQueue>>>;
  initialView: QueueView;
  selectedSaleId: string;
  selectedSale: ApiResult<Sale> | null;
  selectedSection: SaleSection;
  selectedVersion: number | null;
}

// Read in event handlers only; render reads the moment kept in state.
const clockNow = () => Date.now();

// The agent's workspace, laid out as the technician's: queues with counts on the left, the selected sale on the right.
export function SalesWorkspace({
  queues,
  initialView,
  selectedSaleId,
  selectedSale,
  selectedSection,
  selectedVersion,
}: SalesWorkspaceProps) {
  const animateEntry = useContext(StaffPageEntranceContext);
  const toast = useToast();
  const [view, setView] = useState(initialView);
  const [queueExpanded, setQueueExpanded] = useState(!selectedSaleId);
  const [headerActions, setHeaderActions] = useState<HTMLDivElement | null>(null);
  const [draft, setDraft] = useState("");
  // Hold timers are read against this moment, which moves forward each time the queues are refreshed.
  const [now, setNow] = useMinuteClock();
  const handling = useSalesQueue("Handling", queues.Handling ?? null);
  const waiting = useSalesQueue("Waiting", queues.Waiting ?? null);
  const approved = useSalesQueue("Approved", queues.Approved ?? null);
  const claimable = useSalesQueue("ToClaim", queues.ToClaim ?? null);
  const found = useSalesQueue("ToClaim", null);
  const worklists = {
    Handling: handling,
    Waiting: waiting,
    Approved: approved,
    ToClaim: claimable,
  };
  const selection = useSaleSelection(
    selectedSaleId,
    selectedSale,
    selectedSection,
    selectedVersion,
  );
  const busy = queueViews.some((item) => worklists[item].busy) || found.busy;
  const interactive = queueViews.every((item) => worklists[item].ready);
  const chosen = Boolean(selection.id || selection.pendingId);
  const searching = Boolean(found.search);
  // Every read returns all tab counts, so the badges follow the open tab or, before it loads, any loaded tab.
  const counts = [view, ...queueViews]
    .map((item) => worklists[item].snapshot)
    .find(Boolean)?.counts;

  async function refresh() {
    setNow(clockNow());
    const loaded = queueViews.filter((item) => item === view || worklists[item].snapshot);
    const reads = loaded.map((item) => worklists[item].refresh(false));
    if (found.search) {
      reads.push(found.refresh(false));
    }
    if ((await Promise.all(reads)).some((result) => result === false)) {
      toast.error(
        "The sales queues could not all be refreshed. The last confirmed queues remain shown.",
      );
    }
  }

  function showView(value: QueueView) {
    setView(value);
    if (!worklists[value].snapshot) {
      void worklists[value].refresh();
    }
    const url = new URL(window.location.href);
    url.searchParams.set("view", queueViewQueries[value]);
    window.history.replaceState(null, "", url);
  }

  function open(item: SaleSummary) {
    setQueueExpanded(false);
    void selection.choose(item.id);
  }

  return (
    <div className={styles.workspace}>
      <LoadingProgress label="Loading sales queues" active={busy || selection.busy} />
      <StaffCommandBar
        title="Sales"
        actionsRef={setHeaderActions}
        actions={
          !selection.result?.data && (
            <RefreshButton
              label="Refresh sales"
              busy={busy}
              disabled={!interactive || selection.busy}
              onClick={() => void refresh()}
            />
          )
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
              <strong>{searching ? "Search results" : queueViewLabels[view]}</strong>
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
            <form
              className={styles.search}
              onSubmit={(event) => {
                event.preventDefault();
                if (draft.trim()) {
                  void found.find(draft);
                }
              }}
            >
              <SearchField
                label="Search my sales"
                placeholder="Search reference, plate, customer, phone or email"
                value={draft}
                disabled={false}
                busy={found.busy}
                onChange={setDraft}
                onClear={
                  searching
                    ? () => {
                        setDraft("");
                        void found.find("");
                      }
                    : undefined
                }
              />
            </form>
            <nav
              className={styles.queueNavigation}
              aria-label="Sales queue tabs"
              hidden={searching}
            >
              {queueViews.map((item) => (
                <Button
                  key={item}
                  size="compact"
                  variant="ghost"
                  className={styles.queueView}
                  aria-pressed={view === item}
                  title={queueViewLabels[item]}
                  onClick={() => showView(item)}
                >
                  <span>{queueViewTabs[item]}</span>
                  {counts && (
                    <Badge variant="count" data-selected={view === item}>
                      {counts[queueCountOf[item]]}
                      <span className="sr-only"> sales</span>
                    </Badge>
                  )}
                </Button>
              ))}
            </nav>
            {searching && (
              <SalesQueueList
                view="Search"
                title="Search results"
                state={found}
                now={now}
                onOpen={open}
                selectedId={selection.id}
                pendingId={selection.pendingId}
                selectionLocked={selection.locked}
              />
            )}
            {!searching && view === "ToClaim" && (
              <SalesClaim
                state={claimable}
                now={now}
                selectionLocked={selection.locked}
                onConflict={() => void refresh()}
                onClaimed={(saleId) => {
                  setQueueExpanded(false);
                  showView("Handling");
                  void refresh();
                  void selection.choose(saleId);
                }}
              />
            )}
            {!searching && view !== "ToClaim" && (
              <SalesQueueList
                key={view}
                view={view}
                title={queueViewLabels[view]}
                state={worklists[view]}
                now={now}
                onOpen={open}
                selectedId={selection.id}
                pendingId={selection.pendingId}
                selectionLocked={selection.locked}
              />
            )}
          </div>
        </aside>
        <SalesContent
          selection={selection}
          now={now}
          actionsContainer={headerActions}
          onChanged={() => void refresh()}
        />
      </div>
    </div>
  );
}
