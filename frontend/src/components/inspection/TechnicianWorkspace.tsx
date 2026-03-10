"use client";

import { useContext, useState, type ComponentProps } from "react";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { Button } from "@/components/ui/Button";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { useToast } from "@/components/ui/Toast";
import { useInspectionQueue } from "@/hooks/use-inspection-queue";
import { useInspectionDrawer } from "@/hooks/use-inspection-drawer";
import { parseWorkshopPage } from "@/lib/inspection/parse-workshop";
import {
  workshopRowSection,
  type TechnicianTask,
  type WorkshopRow,
} from "@/lib/inspection/workshop-contracts";
import type { ApiResult, Page } from "@/lib/api";
import type { QueueFilters } from "@/lib/inspection/contracts";
import type { WorkshopQueue } from "./WorkshopQueue";
import { TechnicianJobList } from "./TechnicianJobList";
import { TechnicianClaim } from "./TechnicianClaim";
import { TechnicianJobWorkspace } from "./TechnicianJobWorkspace";
import styles from "./TechnicianWorkspace.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export type TechnicianQueueEntry = {
  initial: ApiResult<Page<WorkshopRow>>;
  filters: QueueFilters;
  page: number;
};

type Props = Pick<
  ComponentProps<typeof WorkshopQueue>,
  | "actor"
  | "selectedId"
  | "selectedInspection"
  | "selectedRequest"
  | "selectedSection"
  | "selectedReview"
> & {
  queues: Record<TechnicianTask, TechnicianQueueEntry>;
  initialClaim: boolean;
};

export function TechnicianWorkspace({
  actor,
  queues,
  initialClaim,
  selectedId = "",
  selectedInspection = null,
  selectedRequest = null,
  selectedSection = "inspection",
  selectedReview = null,
}: Props) {
  const animateEntry = useContext(StaffPageEntranceContext);
  const [claim, setClaim] = useState(initialClaim);
  const [queueExpanded, setQueueExpanded] = useState(initialClaim);
  const toast = useToast();
  const ready = useInspectionQueue(
    "workshop",
    queues.MyWork.initial,
    queues.MyWork.filters,
    queues.MyWork.page,
    null,
    parseWorkshopPage,
    "MyWork",
  );
  const waiting = useInspectionQueue(
    "workshop",
    queues.Waiting.initial,
    queues.Waiting.filters,
    queues.Waiting.page,
    null,
    parseWorkshopPage,
    "Waiting",
  );
  const claimable = useInspectionQueue(
    "workshop",
    queues.Claimable.initial,
    queues.Claimable.filters,
    queues.Claimable.page,
    null,
    parseWorkshopPage,
    "Claimable",
  );

  async function refresh(includeClaims = true) {
    const results = await Promise.all([
      ready.refresh(false),
      waiting.refresh(false),
      ...(includeClaims ? [claimable.refresh(false)] : []),
    ]);
    if (results.some((result) => result === false)) {
      toast.error(
        "Workshop jobs could not all be refreshed. The last confirmed worklists remain shown.",
      );
    }
  }

  const selection = useInspectionDrawer(
    selectedId,
    selectedInspection,
    () => void refresh(),
    selectedRequest,
    selectedSection,
  );
  const busy = ready.busy || waiting.busy || claimable.busy;
  const interactive = ready.ready && waiting.ready && claimable.ready;
  const noReadyWork = ready.snapshot?.total === 0 && !ready.active.filters.search;

  function showClaim(value: boolean) {
    setClaim(value);
    setQueueExpanded(value);
    const url = new URL(window.location.href);
    if (value) {
      url.searchParams.set("view", "claim");
    } else {
      url.searchParams.delete("view");
    }
    window.history.replaceState(null, "", url);
  }

  function open(row: WorkshopRow) {
    if (row.inspectionId) {
      setQueueExpanded(false);
      selection.open(row.inspectionId, null, workshopRowSection(row));
    }
  }

  return (
    <div className={styles.workspace}>
      <LoadingProgress label="Loading workshop jobs" active={busy || selection.busy} />
      <StaffCommandBar
        title={claim ? "Claim inspection" : "My work"}
        actions={
          <>
            <Button
              size="compact"
              variant="ghost"
              aria-label={claim ? "Back to my work" : "Claim inspection"}
              disabled={!interactive || selection.locked}
              onClick={() => showClaim(!claim)}
            >
              {claim ? "My work" : "Claim inspection"}
            </Button>
            <RefreshButton
              label="Refresh workshop"
              busy={busy}
              disabled={!interactive}
              onClick={() => void refresh()}
            />
          </>
        }
      />
      <div
        className={`${styles.columns} ${animateEntry ? workbench.reveal : ""}`}
        data-selected={Boolean(selection.id || selection.pendingId)}
        inert={!interactive}
      >
        <aside
          className={styles.queue}
          aria-label="Technician worklists"
          data-expanded={queueExpanded}
        >
          {(selection.id || selection.pendingId) && (
            <div className={styles.mobileQueueBar}>
              <strong>My work</strong>
              <Button
                size="compact"
                variant="secondary"
                aria-expanded={queueExpanded}
                aria-controls="technician-worklist-content"
                onClick={() => setQueueExpanded(!queueExpanded)}
              >
                {queueExpanded ? "Hide jobs" : "Show jobs"}
              </Button>
            </div>
          )}
          <div className={styles.queueBody} id="technician-worklist-content">
            {claim ? (
              <TechnicianClaim
                actor={actor}
                state={claimable}
                onClaimed={(id) => {
                  setQueueExpanded(false);
                  showClaim(false);
                  void refresh();
                  selection.open(id);
                }}
              />
            ) : (
              <>
                {noReadyWork && (
                  <section className={styles.prompt}>
                    <h2>Ready for your next inspection</h2>
                    <p>
                      Your assigned jobs have no actionable work. Claim an inspection to continue.
                    </p>
                    <Button
                      size="compact"
                      disabled={selection.locked}
                      onClick={() => showClaim(true)}
                    >
                      Claim inspection
                    </Button>
                  </section>
                )}
                {!noReadyWork && (
                  <TechnicianJobList
                    title="Ready to continue"
                    state={ready}
                    onOpen={open}
                    selectedId={selection.id}
                    pendingId={selection.pendingId}
                    selectionLocked={selection.locked}
                  />
                )}
                <TechnicianJobList
                  title="Waiting for approval"
                  state={waiting}
                  onOpen={open}
                  selectedId={selection.id}
                  pendingId={selection.pendingId}
                  selectionLocked={selection.locked}
                />
              </>
            )}
          </div>
        </aside>
        <TechnicianJobWorkspace
          actor={actor}
          selection={selection}
          selectedReview={selectedReview}
          onChanged={() => void refresh(false)}
        />
      </div>
    </div>
  );
}
