"use client";

import { useContext, useState } from "react";
import {
  useIntakeWorkspace,
  type IntakeWorkspaceSnapshot,
  type IntakeViewComponent,
} from "@/hooks/use-intake-workspace";
import { useInitialToast } from "@/hooks/use-initial-toast";
import type { CurrentActor } from "@/lib/auth/actor";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { IntakeAppointmentDrawer } from "./IntakeAppointmentDrawer";
import { StaffCommandBar } from "./StaffPage";
import { StaffPageEntranceContext } from "./StaffActorBoundary";
import layout from "./StaffWorkspace.module.css";
import styles from "./StaffWorklist.module.css";
import { IntakeAgentContent } from "./IntakeAgentContent";

interface Props {
  actor: CurrentActor;
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

function Workspace({ actor, initial, initialView: content }: Props) {
  const animateEntry = useContext(StaffPageEntranceContext);
  const [headerActions, setHeaderActions] = useState<HTMLDivElement | null>(null);
  const workspace = useIntakeWorkspace(initial, actor.role === "SalesAgent");
  const { worklist, drawer } = workspace;
  const view = content.view;
  const busy = worklist.busy;
  useInitialToast(
    actor.role === "GeneralManager" && !initial.branches
      ? "Branch choices are unavailable. All-branch access remains available."
      : null,
  );
  function refresh() {
    worklist.cancel();
    return worklist.load(worklist.active.filters, worklist.active.page);
  }

  return (
    <div className={styles.workspace} inert={workspace.historyBusy}>
      <LoadingProgress label="Loading appointments" active={busy || drawer.busy} />
      <StaffCommandBar
        title="Intake appointments"
        actionsRef={setHeaderActions}
        actions={
          <>
            {(view !== "queue" || (!drawer.id && !drawer.pendingId)) && (
              <RefreshButton
                label="Refresh worklist"
                busy={busy}
                disabled={Boolean(drawer.id) || !worklist.ready}
                onClick={() => void refresh()}
              />
            )}
          </>
        }
      />
      {content.view === "queue" ? (
        <content.Component
          worklist={worklist}
          drawer={drawer}
          className={animateEntry ? layout.reveal : ""}
        >
          <IntakeAgentContent
            actor={actor}
            selection={drawer}
            actionsContainer={headerActions}
            onConfirmed={workspace.onConfirmed}
          />
        </content.Component>
      ) : (
        <div className={`${styles.content} ${animateEntry ? layout.reveal : ""}`}>
          {content.view === "list" && (
            <content.Component actor={actor} worklist={worklist} drawer={drawer} />
          )}
        </div>
      )}
      {view !== "queue" && (
        <IntakeAppointmentDrawer actor={actor} drawer={drawer} onConfirmed={() => void refresh()} />
      )}
    </div>
  );
}
