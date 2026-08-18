"use client";

import { StaffQueuePlaceholder } from "./StaffQueuePlaceholder";
import { Button } from "@/components/ui/Button";
import type { useStaffAppointmentDrawer } from "@/hooks/use-staff-appointment-drawer";
import type { CurrentActor } from "@/lib/auth/actor";
import type { AppointmentDetail } from "@/lib/staff/contracts";
import { AppointmentWorkbench } from "./AppointmentWorkbench";
import { StaffDrawerLoadingShell } from "./StaffDrawerLoadingShell";
import styles from "./StaffQueueWorkspace.module.css";

export function IntakeAgentContent({
  actor,
  selection,
  actionsContainer,
  onConfirmed,
  retryTraversal,
  onGoToClaim,
}: {
  actor: CurrentActor;
  selection: ReturnType<typeof useStaffAppointmentDrawer>;
  actionsContainer: HTMLElement | null;
  onConfirmed: (detail: AppointmentDetail) => void;
  retryTraversal: () => boolean;
  // Opens the To claim queue, which the empty content pane points to.
  onGoToClaim: () => void;
}) {
  if (!selection.id && !selection.pendingId) {
    return (
      <StaffQueuePlaceholder
        label="Appointment content"
        title="Select an appointment"
        link={{ text: "To claim", onClick: onGoToClaim }}
      >
        Open one from your queue, or claim one in To claim.
      </StaffQueuePlaceholder>
    );
  }
  return (
    <section
      className={styles.content}
      aria-label="Selected appointment"
      aria-busy={selection.busy}
    >
      <header className={styles.contentHeader}>
        <div>
          <h2>{selection.result?.data?.registrationPlate ?? "Intake appointment"}</h2>
          {selection.result?.data && (
            <p>
              {selection.result.data.manufactureYear} {selection.result.data.make}{" "}
              {selection.result.data.model} {selection.result.data.trim} ·{" "}
              {selection.result.data.branchName}
            </p>
          )}
        </div>
      </header>
      {selection.busy && !selection.result?.data && (
        <StaffDrawerLoadingShell label="Loading appointment…" fact single tabs={false} />
      )}
      {selection.result?.data && (
        <div className={styles.editor} inert={selection.busy}>
          <AppointmentWorkbench
            key={selection.id}
            id={selection.id}
            actor={actor}
            initial={selection.result}
            actionsContainer={actionsContainer}
            draft={selection.draft}
            onDraft={selection.setDraft}
            onLock={selection.lock}
            onConfirmed={onConfirmed}
            retryTraversal={retryTraversal}
            scrollWithHost
          />
        </div>
      )}
      {!selection.busy && !selection.result?.data && (
        <div className={styles.empty}>
          <Button size="compact" variant="secondary" onClick={() => selection.open(selection.id)}>
            Retry appointment
          </Button>
        </div>
      )}
    </section>
  );
}
