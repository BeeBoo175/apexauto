"use client";

import type { ComponentProps } from "react";
import { Button } from "@/components/ui/Button";
import type { useInspectionDrawer } from "@/hooks/use-inspection-drawer";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SelectedRepairReview } from "@/lib/inspection/repair-review";
import { InspectionEditor } from "./InspectionEditor";
import { WorkshopLoadingShell } from "./WorkshopLoadingShell";
import styles from "./TechnicianWorkspace.module.css";

export function TechnicianJobWorkspace({
  actor,
  selection,
  selectedReview,
  actionsContainer,
  refreshControl,
  onChanged,
}: {
  actor: CurrentActor;
  selection: ReturnType<typeof useInspectionDrawer>;
  selectedReview: SelectedRepairReview | null;
  actionsContainer: HTMLElement | null;
  refreshControl: ComponentProps<typeof InspectionEditor>["refreshControl"];
  onChanged: () => void;
}) {
  const vehicle = selection.result?.data?.draft.vehicle;
  if (!selection.id && !selection.pendingId) {
    return (
      <section className={styles.placeholder} aria-label="Job content">
        <h2>Select a job</h2>
        <p>Choose an assigned job to continue, or claim an available vehicle.</p>
      </section>
    );
  }
  if (selection.busy && !selection.result?.data) {
    return (
      <section className={styles.content} aria-label="Job content" aria-busy="true">
        <WorkshopLoadingShell />
      </section>
    );
  }
  return (
    <section
      className={styles.content}
      aria-label="Selected workshop job"
      aria-busy={selection.busy}
      inert={selection.busy}
    >
      <header className={styles.contentHeader}>
        <div>
          <h2>{vehicle?.registrationPlate ?? "Workshop job"}</h2>
          {vehicle && (
            <p>
              {vehicle.make} {vehicle.model} · {vehicle.branchName}
            </p>
          )}
        </div>
      </header>
      {selection.result?.data && (
        <div className={styles.editor}>
          <InspectionEditor
            key={selection.id}
            id={selection.id}
            actor={actor}
            initial={selection.result}
            technicians={null}
            actionsContainer={actionsContainer}
            refreshControl={refreshControl}
            scrollWithHost
            section={selection.section}
            onSection={selection.changeSection}
            onReview={selection.review}
            selectedRequest={selection.request}
            selectedReview={selectedReview}
            onLocked={selection.lock}
            onChanged={onChanged}
          />
        </div>
      )}
      {!selection.busy && !selection.result?.data && (
        <div className={styles.empty}>
          <p>This job is unavailable. Your worklists remain retained.</p>
          <Button
            size="compact"
            variant="secondary"
            onClick={() => void selection.choose(selection.id)}
          >
            Retry job
          </Button>
        </div>
      )}
    </section>
  );
}
