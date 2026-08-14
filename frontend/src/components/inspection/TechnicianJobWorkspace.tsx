"use client";

import { StaffQueuePlaceholder } from "@/components/staff/StaffQueuePlaceholder";
import type { ComponentProps } from "react";
import { Button } from "@/components/ui/Button";
import type { useInspectionDrawer } from "@/hooks/use-inspection-drawer";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SelectedRepairReview } from "@/lib/inspection/repair-review";
import { InspectionEditor } from "./InspectionEditor";
import { StaffDrawerLoadingShell } from "@/components/staff/StaffDrawerLoadingShell";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";

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
      <StaffQueuePlaceholder label="Job content" title="Select a job">
        Choose an assigned job to continue, or claim an available vehicle.
      </StaffQueuePlaceholder>
    );
  }
  if (selection.busy && !selection.result?.data) {
    return (
      <section className={styles.content} aria-label="Job content" aria-busy="true">
        <StaffDrawerLoadingShell label="Loading workshop record…" fact />
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
            category={selection.category}
            onCategory={selection.changeCategory}
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
