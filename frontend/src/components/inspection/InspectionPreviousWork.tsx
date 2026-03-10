"use client";

import { Button } from "@/components/ui/Button";
import type { useInspectionHistory } from "@/hooks/use-inspection-history";
import type { RepairHistory } from "@/lib/inspection/report-contracts";
import { InspectionHistoryProgress } from "./InspectionHistory";
import { formatCurrency } from "@/lib/currency-format";
import type { RepairSelection } from "@/lib/inspection/query";
import styles from "./InspectionTimeline.module.css";

export function InspectionPreviousWork({
  revision,
  read,
  disabled,
  onSelect,
}: {
  revision: number;
  read: ReturnType<typeof useInspectionHistory<RepairHistory>>;
  disabled: boolean;
  onSelect: (selection: RepairSelection) => void;
}) {
  const { history, busy } = read;
  return (
    <section
      className={styles.paging}
      aria-label={`Previous repair work · revision ${revision}`}
      aria-busy={busy}
    >
      <h3>Previous repair work · revision {revision}</h3>
      {history?.totalCount === 0 && <p>No repair requests for this revision.</p>}
      <ul className={styles.timeline}>
        {history?.items.map(({ request, approval, completion }) => (
          <li key={request.operationId}>
            <strong>
              Work version {request.version} · {formatCurrency(request.budget)}
            </strong>
            <span>
              {completion ? "Completed" : "Retained request"}
              {approval ? " · Approved" : ""}
            </span>
            <Button
              size="compact"
              variant="secondary"
              disabled={disabled}
              onClick={() => onSelect({ requestId: request.operationId, revision })}
            >
              View work and signed findings
            </Button>
          </li>
        ))}
      </ul>
      <InspectionHistoryProgress read={read} label="Previous repair requests" disabled={disabled} />
    </section>
  );
}
