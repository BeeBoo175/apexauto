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
import styles from "./IntakeWorkspace.module.css";
import layout from "./StaffWorkspace.module.css";

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
  const workspace = useIntakeWorkspace(initial, initialView);
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
    return worklist.load();
  }

  return (
    <div className={styles.workspace}>
      <LoadingProgress label="Loading appointments" active={busy || Boolean(pending)} />
      <StaffCommandBar
        title="Intake appointments"
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
              active={view}
              pending={pending}
              destinations={destinations}
              disabled={Boolean(drawer.id) || !calendar.ready || !worklist.ready}
              onNavigate={(href) => void workspace.navigate(href)}
              onPrepare={workspace.prefetchView}
              onCancel={workspace.cancelNavigation}
            />
          </>
        }
      />
      {pending && (
        <p className="sr-only" role="status">
          Loading {pending === "calendar" ? "calendar" : "table"}…
        </p>
      )}
      <div
        className={`${styles.content} ${animateEntry ? layout.reveal : ""}`}
        inert={Boolean(pending)}
        aria-busy={Boolean(pending)}
      >
        {content.view === "calendar" ? (
          <content.Component actor={actor} today={today} calendar={calendar} drawer={drawer} />
        ) : (
          <content.Component actor={actor} worklist={worklist} drawer={drawer} />
        )}
      </div>
      <IntakeAppointmentDrawer actor={actor} drawer={drawer} onConfirmed={() => void refresh()} />
    </div>
  );
}
