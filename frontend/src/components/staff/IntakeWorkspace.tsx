"use client";

import { useContext, useState } from "react";
import {
  useIntakeWorkspace,
  type IntakeWorkspaceSnapshot,
  type IntakeViewComponent,
} from "@/hooks/use-intake-workspace";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { IntakeViewNavigation } from "./IntakeViewNavigation";
import { IntakeAppointmentDrawer } from "./IntakeAppointmentDrawer";
import { StaffCommandBar } from "./StaffPage";
import { StaffPageEntranceContext } from "./StaffActorBoundary";
import layout from "./StaffWorkspace.module.css";
import styles from "./StaffWorklist.module.css";
import queueStyles from "./StaffQueueWorkspace.module.css";
import { IntakeAgentContent } from "./IntakeAgentContent";

interface Props {
  actor: CurrentActor;
  today: string;
  initial: IntakeWorkspaceSnapshot;
  initialView: IntakeViewComponent;
}

export function IntakeWorkspace(props: Props) {
  const [entry, setEntry] = useState({ initial: props.initial, revision: 0 });
  if (entry.initial !== props.initial) {
    setEntry({ initial: props.initial, revision: entry.revision + 1 });
  }
  return <Workspace key={entry.revision} {...props} />;
}

function Workspace({ actor, today, initial, initialView }: Props) {
  const animateEntry = useContext(StaffPageEntranceContext);
  const [headerActions, setHeaderActions] = useState<HTMLDivElement | null>(null);
  const workspace = useIntakeWorkspace(initial, initialView, actor.role === "SalesAgent");
  const { view, content, pending, busy, calendar, worklist, drawer, destinations } = workspace;
  useInitialToast(
    actor.role === "GeneralManager" && !initial.branches
      ? "Branch choices are unavailable. All-branch access remains available."
      : null,
  );
  function refresh() {
    if (view === "calendar") {
      calendar.cancel();
      return calendar.load();
    }
    worklist.cancel();
    return worklist.load(worklist.active.filters, view === "queue" ? 1 : worklist.active.page);
  }

  return (
    <div className={styles.workspace}>
      <LoadingProgress
        label="Loading appointments"
        active={busy || Boolean(pending) || drawer.busy}
      />
      <StaffCommandBar
        title="Intake appointments"
        actionsRef={setHeaderActions}
        actions={
          <>
            <RefreshButton
              label={view === "calendar" ? "Refresh calendar" : "Refresh worklist"}
              busy={busy && !pending}
              disabled={
                Boolean(drawer.id) || Boolean(pending) || !calendar.ready || !worklist.ready
              }
              onClick={() => void refresh()}
            />
            <IntakeViewNavigation
              actor={actorIdentity(actor)}
              agent={actor.role === "SalesAgent"}
              active={view}
              pending={pending}
              destinations={destinations}
              disabled={
                (actor.role !== "SalesAgent" && Boolean(drawer.id)) ||
                !calendar.ready ||
                !worklist.ready
              }
              onNavigate={(href) => void workspace.navigate(href)}
              onPrepare={workspace.prefetchView}
              onCancel={workspace.cancelNavigation}
            />
          </>
        }
      />
      {pending && (
        <p className="sr-only" role="status">
          Loading appointments…
        </p>
      )}
      <div
        className={`${view === "queue" ? queueStyles.columns : styles.content} ${animateEntry ? layout.reveal : ""}`}
        data-selected={Boolean(drawer.id || drawer.pendingId)}
        inert={Boolean(pending)}
        aria-busy={Boolean(pending)}
      >
        {content.view === "calendar" && (
          <content.Component actor={actor} today={today} calendar={calendar} drawer={drawer} />
        )}
        {content.view === "list" && (
          <content.Component actor={actor} worklist={worklist} drawer={drawer} />
        )}
        {content.view === "queue" && (
          <>
            <content.Component worklist={worklist} drawer={drawer} />
            <IntakeAgentContent
              actor={actor}
              selection={drawer}
              actionsContainer={headerActions}
              onConfirmed={() => void refresh()}
            />
          </>
        )}
      </div>
      {view !== "queue" && (
        <IntakeAppointmentDrawer actor={actor} drawer={drawer} onConfirmed={() => void refresh()} />
      )}
    </div>
  );
}
