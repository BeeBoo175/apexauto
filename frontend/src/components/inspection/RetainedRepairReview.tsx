"use client";

import { createPortal } from "react-dom";
import type { ComponentProps } from "react";
import { useRepairReview } from "@/hooks/use-repair-review";
import type { RepairSelection } from "@/hooks/use-inspection-drawer";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { Button } from "@/components/ui/Button";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { RepairRequestDetails, RepairRequestMetadata } from "./RepairRequestDetails";
import styles from "./Inspection.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import { AppointmentLoadingShell } from "@/components/staff/AppointmentLoadingShell";
import type { ApiResult } from "@/lib/api";
import type { RepairReview } from "@/lib/inspection/repair-review";

const requestDetails = ["Requested budget", "Requested", "Signed revision", "Work version"];

export function RetainedRepairReview({
  inspectionId,
  selection,
  actionsContainer,
  refreshControl,
  initial = null,
}: {
  inspectionId: string;
  selection: RepairSelection;
  actionsContainer?: HTMLElement | null;
  refreshControl?: ComponentProps<typeof RefreshButton>;
  initial?: ApiResult<RepairReview> | null;
}) {
  const review = useRepairReview(inspectionId, selection, initial);
  const approval = review.result?.entry.approval;
  const completion = review.result?.entry.completion;
  return (
    <section
      className={`${styles.section} ${workbench.stack}`}
      aria-label="Selected retained repair request"
      aria-busy={review.busy}
    >
      <LoadingProgress label="Loading selected repair request" active={review.busy} />
      {actionsContainer &&
        createPortal(
          <RefreshButton
            label={refreshControl?.label ?? "Refresh repair request"}
            busy={review.busy || Boolean(refreshControl?.busy)}
            disabled={Boolean(refreshControl?.disabled)}
            onClick={() => {
              refreshControl?.onClick();
              review.retry();
            }}
          />,
          actionsContainer,
        )}
      <div className={`${workbench.toolbar} ${styles.repairHeading}`}>
        <h2 className={workbench.sectionTitle}>Retained repair request</h2>
        {review.result && <RepairRequestMetadata request={review.result.entry.request} />}
      </div>
      {review.busy && !review.result && (
        <AppointmentLoadingShell
          label="Loading selected work and signed findings…"
          details={requestDetails}
        />
      )}
      {!review.busy && !review.result && (
        <p role="alert">The selected request could not be loaded. Use refresh to retry.</p>
      )}
      {review.result && (
        <RepairRequestDetails request={review.result.entry.request} report={review.result.report} />
      )}
      {approval && <p>Approved {formatMalaysianDateTime(approval.approvedAt)}</p>}
      {completion && (
        <p>
          Completed {formatMalaysianDateTime(completion.completedAt)} · {completion.notes}
        </p>
      )}
      {!actionsContainer && (
        <Button size="compact" variant="secondary" disabled={review.busy} onClick={review.retry}>
          Retry selected request
        </Button>
      )}
    </section>
  );
}
