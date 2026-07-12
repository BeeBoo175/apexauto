"use client";

import { useContext, useState, type ComponentProps } from "react";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffQueueLayout } from "@/components/staff/StaffQueueLayout";
import { SearchField } from "@/components/ui/SearchField";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { useToast } from "@/components/ui/Toast";
import { useTechnicianQueue } from "@/hooks/use-technician-queue";
import { useInspectionDrawer } from "@/hooks/use-inspection-drawer";
import { useGuardedAction } from "@/hooks/use-guarded-action";
import {
  workshopRowSection,
  type TechnicianTask,
  type WorkshopRow,
  type WorkshopJobs,
} from "@/lib/inspection/workshop-contracts";
import type { ApiResult } from "@/lib/api";
import type { WorkshopQueue } from "./WorkshopQueue";
import { TechnicianJobList } from "./TechnicianJobList";
import { TechnicianClaim } from "./TechnicianClaim";
import { TechnicianJobWorkspace } from "./TechnicianJobWorkspace";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import { writeAddress } from "@/lib/browser-url";

const viewLabels: Record<TechnicianTask, string> = {
  MyWork: "Ready to work",
  Waiting: "Awaiting approval",
  Claimable: "Available to claim",
};

const viewTabs: Record<TechnicianTask, string> = {
  MyWork: "Ready",
  Waiting: "Waiting",
  Claimable: "To claim",
};

const viewQueries: Record<TechnicianTask, string> = {
  MyWork: "ready",
  Waiting: "waiting",
  Claimable: "claim",
};

export type TechnicianQueueEntry = {
  initial: ApiResult<WorkshopJobs>;
  search: string;
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
  initialView: TechnicianTask;
};

export function TechnicianWorkspace({
  actor,
  queues,
  initialView,
  selectedId = "",
  selectedInspection = null,
  selectedRequest = null,
  selectedSection = "inspection",
  selectedReview = null,
}: Props) {
  const animateEntry = useContext(StaffPageEntranceContext);
  const interaction = useGuardedAction();
  const [view, setView] = useState(initialView);
  const [queueExpanded, setQueueExpanded] = useState(initialView !== "MyWork");
  const [headerActions, setHeaderActions] = useState<HTMLDivElement | null>(null);
  const toast = useToast();
  const ready = useTechnicianQueue("MyWork", queues.MyWork.initial, queues.MyWork.search);
  const waiting = useTechnicianQueue("Waiting", queues.Waiting.initial, queues.Waiting.search);
  const claimable = useTechnicianQueue(
    "Claimable",
    queues.Claimable.initial,
    queues.Claimable.search,
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
  const worklists = { MyWork: ready, Waiting: waiting, Claimable: claimable };
  const refreshControl = {
    label: "Refresh workshop",
    busy,
    disabled: !interactive || selection.busy,
    onClick: () => void refresh(),
  };

  function showView(value: TechnicianTask) {
    setView(value);
    const url = new URL(window.location.href);
    url.searchParams.set("view", viewQueries[value]);
    writeAddress(url);
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
        title="Workshop"
        actionsRef={setHeaderActions}
        actions={!selection.result?.data && <RefreshButton {...refreshControl} />}
      />
      <StaffQueueLayout
        id="technician-worklist-content"
        label="Technician worklists"
        title={viewLabels[view]}
        selected={Boolean(selection.id || selection.pendingId)}
        expanded={queueExpanded}
        onExpandedChange={setQueueExpanded}
        className={animateEntry ? workbench.reveal : ""}
        inert={!interactive}
        tabs={
          <StaffQueueTabs
            label="Workshop worklists"
            layout="queue"
            countNoun="jobs"
            tabs={(["MyWork", "Waiting", "Claimable"] as const).map((task) => ({
              value: task,
              label: viewTabs[task],
              count: worklists[task].snapshot?.total,
            }))}
            value={view}
            disabled={interaction.blocked}
            onChange={showView}
          />
        }
        search={
          <form
            className={styles.search}
            onSubmit={(event) => {
              event.preventDefault();
              if (!worklists[view].busy) {
                void worklists[view].submit();
              }
            }}
          >
            <SearchField
              label={`Search ${viewLabels[view].toLowerCase()}`}
              placeholder="Search vehicle or registration"
              value={worklists[view].draft}
              disabled={!worklists[view].ready || interaction.blocked}
              busy={worklists[view].busy}
              onChange={worklists[view].setDraft}
              onClear={
                worklists[view].search && !busy ? () => void worklists[view].clear() : undefined
              }
            />
          </form>
        }
        queue={
          view === "Claimable" ? (
            <TechnicianClaim
              actor={actor}
              state={claimable}
              selectionLocked={selection.locked}
              onClaimed={(id) => {
                setQueueExpanded(false);
                showView("MyWork");
                void refresh();
                selection.open(id);
              }}
            />
          ) : (
            <TechnicianJobList
              key={view}
              title={viewLabels[view]}
              state={worklists[view]}
              onOpen={open}
              selectedId={selection.id}
              pendingId={selection.pendingId}
              selectionLocked={interaction.blocked}
            />
          )
        }
      >
        <TechnicianJobWorkspace
          actor={actor}
          selection={selection}
          selectedReview={selectedReview}
          actionsContainer={headerActions}
          refreshControl={refreshControl}
          onChanged={() => void refresh(false)}
        />
      </StaffQueueLayout>
    </div>
  );
}
