"use client";

import { useContext, useState } from "react";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { StaffQueueLayout } from "@/components/staff/StaffQueueLayout";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { SearchField } from "@/components/ui/SearchField";
import { useGuardedAction } from "@/hooks/use-guarded-action";
import { useMinuteClock } from "@/hooks/use-minute-clock";
import { useSalesAgent } from "@/hooks/use-sales-agent";
import type { ApiResult } from "@/lib/api";
import {
  type Sale,
  queueCountOf,
  queueViewLabels,
  queueViewTabs,
  queueViews,
  type QueueView,
  type SaleSummary,
  type SalesQueue,
} from "@/lib/sales/contracts";
import type { SaleSection } from "@/lib/sales/sale-sections";
import type { AgentList } from "@/lib/sales/sales-location";
import { SalesClaim } from "./SalesClaim";
import { SalesContent } from "./SalesContent";
import { SalesQueueList } from "./SalesQueueList";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export interface SalesWorkspaceProps {
  // The opening tab is read on entry; the others are read the first time they are shown.
  queues: Partial<Record<QueueView, ApiResult<SalesQueue>>>;
  found: ApiResult<SalesQueue> | null;
  list: AgentList & { view: QueueView };
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
  found: foundQueue,
  list,
  selectedSaleId,
  selectedSale,
  selectedSection,
  selectedVersion,
}: SalesWorkspaceProps) {
  const animateEntry = useContext(StaffPageEntranceContext);
  const interaction = useGuardedAction();
  const [queueExpanded, setQueueExpanded] = useState(!selectedSaleId);
  const [headerActions, setHeaderActions] = useState<HTMLDivElement | null>(null);
  const [draft, setDraft] = useState("");
  // Hold timers are read against this moment, which moves forward each time the queues are refreshed.
  const [now, setNow] = useMinuteClock();
  const {
    view,
    showView: showViewTab,
    worklists,
    found,
    drawer: selection,
    refreshQueues,
    refreshDestination,
  } = useSalesAgent({
    queues,
    found: foundQueue,
    list,
    selectedSaleId,
    selectedSale,
    selectedSection,
    selectedVersion,
  });
  const claimable = worklists.ToClaim;
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
    await refreshQueues();
  }

  function showView(value: QueueView) {
    if (interaction.blocked) {
      return;
    }
    showViewTab(value);
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
              onClick={() => void refreshDestination()}
            />
          )
        }
      />
      <StaffQueueLayout
        id="sales-queue-content"
        label="Sales queues"
        title={searching ? "Search results" : queueViewLabels[view]}
        selected={chosen}
        expanded={queueExpanded}
        onExpandedChange={setQueueExpanded}
        className={animateEntry ? workbench.reveal : ""}
        inert={!interactive}
        tabs={
          <div hidden={searching}>
            <StaffQueueTabs
              label="Sales queue tabs"
              layout="queue"
              countNoun="sales"
              tabs={queueViews.map((item) => ({
                value: item,
                label: queueViewTabs[item],
                count: counts?.[queueCountOf[item]],
              }))}
              value={view}
              disabled={interaction.blocked}
              onChange={showView}
            />
          </div>
        }
        search={
          <form
            className={styles.search}
            onSubmit={(event) => {
              event.preventDefault();
              if (!interaction.blocked && draft.trim()) {
                void found.find(draft);
              }
            }}
          >
            <SearchField
              label="Search my sales"
              placeholder="Search reference, plate, customer, phone or email"
              value={draft}
              disabled={interaction.blocked}
              disabledReason={interaction.message}
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
        }
        queue={
          <>
            {searching && (
              <SalesQueueList
                view="Search"
                title="Search results"
                state={found}
                now={now}
                onOpen={open}
                selectedId={selection.id}
                pendingId={selection.pendingId}
                selectionLocked={interaction.blocked}
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
                selectionLocked={interaction.blocked}
              />
            )}
          </>
        }
      >
        <SalesContent
          selection={selection}
          now={now}
          actionsContainer={headerActions}
          onChanged={() => void refresh()}
        />
      </StaffQueueLayout>
    </div>
  );
}
