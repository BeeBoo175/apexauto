"use client";

import type { ComponentProps } from "react";
import { createPortal } from "react-dom";
import { StaffRecordLoadingShell } from "@/components/staff/StaffRecordLoadingShell";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import type { useSaleVersion } from "@/hooks/use-sale-version";
import type { SaleContext } from "@/lib/sales/contracts";
import type { SaleSection } from "@/lib/sales/sale-sections";
import { historyVersionTitle } from "@/lib/sales/sale-history";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { SaleFacts } from "./SaleFacts";
import { SaleDeal } from "./SaleDeal";
import { SaleCompletionEvidence } from "./SaleCompletionEvidence";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import record from "@/components/staff/StaffRecordContent.module.css";
import styles from "./Sales.module.css";

const loadingDetails = ["Status", "Price", "Handling"];

// The retained record shares the live section's composition without changing any local draft.
export function SalePastVersion({
  review,
  section,
  context,
  actionsContainer,
  refreshControl,
}: {
  review: ReturnType<typeof useSaleVersion>;
  section: SaleSection;
  context: SaleContext;
  actionsContainer?: HTMLElement | null;
  refreshControl?: ComponentProps<typeof RefreshButton>;
}) {
  const { result } = review;
  let title = "Retained deal";
  if (section === "customer") {
    title = "Customer reference";
  } else if (result) {
    title = historyVersionTitle({ kind: result.kind, toStatus: result.snapshot.status });
  }
  return (
    <section className={styles.section} aria-label="Past version" aria-busy={review.busy}>
      <LoadingProgress label="Loading the past version" active={review.busy} />
      {actionsContainer &&
        createPortal(
          <RefreshButton
            label="Refresh sale version"
            busy={review.busy || Boolean(refreshControl?.busy)}
            disabled={Boolean(refreshControl?.disabled)}
            onClick={() => {
              review.retry();
              refreshControl?.onClick();
            }}
          />,
          actionsContainer,
        )}
      <div className={workbench.sectionHeader}>
        <h2 className={workbench.sectionTitle}>
          {title}
          {result && ` · version ${result.version}`}
        </h2>
      </div>
      {result && (
        <p className={workbench.help}>Recorded {formatMalaysianDateTime(result.recordedAt)}</p>
      )}
      {result && section === "customer" && (
        <p className={workbench.help}>
          Contact details reflect the current customer record. The sale summary reflects version{" "}
          {result.version}.
        </p>
      )}
      {review.busy && !result && (
        <StaffRecordLoadingShell label="Loading the sale as it stood…" details={loadingDetails} />
      )}
      {review.failed && (
        <div className={workbench.protocol} role="alert">
          <p>This version could not be loaded.</p>
          <Button size="compact" variant="secondary" onClick={review.retry}>
            Retry version
          </Button>
        </div>
      )}
      {result && section === "customer" && <SaleFacts context={context} />}
      {result && section !== "customer" && (
        <>
          <SaleDeal sale={result.snapshot} context={context} />
          <SaleCompletionEvidence sale={result.snapshot} />
          {result.reason && (
            <section className={record.group}>
              <h4>Reason</h4>
              <p>{result.reason}</p>
            </section>
          )}
        </>
      )}
    </section>
  );
}
