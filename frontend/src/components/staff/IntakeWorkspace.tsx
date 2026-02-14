"use client";

import { useState } from "react";
import { useIntakeWorkspace, type IntakeWorkspaceSnapshot } from "@/hooks/use-intake-workspace";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { IntakeCalendar } from "./IntakeCalendar";
import { IntakeWorklist } from "./IntakeWorklist";
import { IntakeViewNavigation } from "./IntakeViewNavigation";
import { IntakeAppointmentDrawer } from "./IntakeAppointmentDrawer";
import { StaffCommandBar } from "./StaffPage";
import styles from "./IntakeWorkspace.module.css";

interface Props {
  actor: CurrentActor;
  today: string;
  initial: IntakeWorkspaceSnapshot;
}

export function IntakeWorkspace(props: Props) {
  const [entry, setEntry] = useState({ initial: props.initial, revision: 0 });
  if (entry.initial !== props.initial) {
    setEntry({ initial: props.initial, revision: entry.revision + 1 });
  }
  return <Workspace key={entry.revision} {...props} />;
}

function Workspace({ actor, today, initial }: Props) {
  const workspace = useIntakeWorkspace(initial);
  const { view, pending, busy, calendar, worklist, drawer, destinations } = workspace;
  useInitialToast(
    actor.role === "GeneralManager" && !initial.branches
      ? "Branch choices are unavailable. All-branch access remains available."
      : null,
  );
  function refresh() {
    return view === "calendar" ? calendar.load() : worklist.load();
  }

  return (
    <div className={styles.workspace}>
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
              disabled={Boolean(drawer.id) || busy}
              onNavigate={(href) => void workspace.navigate(href)}
            />
          </>
        }
      />
      {pending && (
        <p className="sr-only" role="status">
          Loading {pending === "calendar" ? "calendar" : "table"}…
        </p>
      )}
      <div className={styles.content} inert={Boolean(pending)} aria-busy={Boolean(pending)}>
        {view === "calendar" ? (
          <IntakeCalendar actor={actor} today={today} calendar={calendar} drawer={drawer} />
        ) : (
          <IntakeWorklist actor={actor} worklist={worklist} drawer={drawer} />
        )}
      </div>
      <IntakeAppointmentDrawer
        actor={actor}
        drawer={drawer}
        refreshing={busy}
        onConfirmed={() => void refresh()}
      />
    </div>
  );
}
