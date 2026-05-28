"use client";

import { Button } from "@/components/ui/Button";
import type { useStaffAppointmentDrawer } from "@/hooks/use-staff-appointment-drawer";
import type { CurrentActor } from "@/lib/auth/actor";
import { AppointmentWorkbench } from "./AppointmentWorkbench";
import { StaffRecordLoadingShell } from "./StaffRecordLoadingShell";
import styles from "./StaffQueueWorkspace.module.css";
import workbench from "./StaffWorkspace.module.css";

export function IntakeAgentContent({
  actor,
  selection,
  actionsContainer,
  onConfirmed,
}: {
  actor: CurrentActor;
  selection: ReturnType<typeof useStaffAppointmentDrawer>;
  actionsContainer: HTMLElement | null;
  onConfirmed: () => void;
}) {
  if (!selection.id && !selection.pendingId) {
    return (
      <section className={styles.placeholder} aria-label="Appointment content">
        <h2>Select an appointment</h2>
        <p>Choose a branch appointment to claim, or open your work to record its arrival.</p>
      </section>
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
              {selection.result.data.make} {selection.result.data.model} ·{" "}
              {selection.result.data.branchName}
            </p>
          )}
        </div>
      </header>
      {selection.busy && !selection.result?.data && (
        <StaffRecordLoadingShell
          label="Loading appointment…"
          details={["Branch", "Customer", "Inspection", "Booked vehicle", "Declared mileage"]}
          showForm
        />
      )}
      {selection.result?.data && (
        <div className={workbench.drawerBody} inert={selection.busy}>
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
