"use client";

import type { useInspectionHistory } from "@/hooks/use-inspection-history";
import type { RepairHistory } from "@/lib/inspection/report-contracts";
import { InspectionHistoryProgress, InspectionRepairLink } from "./InspectionHistory";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
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
      aria-label={`Repair requests for inspection ${revision}`}
      aria-busy={busy}
    >
      <h3>Repair requests</h3>
      {history?.totalCount === 0 && <p>No repair requests were raised for this inspection.</p>}
      <ul className={styles.timeline}>
        {history?.items.map(({ request, approval, completion }) => (
          <li key={request.operationId}>
            <strong>Repair request · {formatCurrency(request.budget)}</strong>
            <time dateTime={request.requestedAt}>
              {formatMalaysianDateTime(request.requestedAt)}
            </time>
            <span>
              {completion ? "Completed" : "Retained request"}
              {approval ? " · Approved" : ""}
            </span>
            <InspectionRepairLink
              inspectionId={request.inspectionId}
              selection={{ requestId: request.operationId, revision }}
              disabled={disabled}
              onSelect={onSelect}
            />
          </li>
        ))}
      </ul>
      <InspectionHistoryProgress read={read} label="Previous repair requests" disabled={disabled} />
    </section>
  );
}
