"use client";

import { Button } from "@/components/ui/Button";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import { useInspectionHistory } from "@/hooks/use-inspection-history";
import { parseRepairs } from "@/lib/inspection/parse-report";
import { formatCurrency } from "@/lib/currency-format";
import type { RepairSelection } from "@/lib/inspection/query";
import styles from "./InspectionTimeline.module.css";

export function InspectionPreviousWork({
  inspectionId,
  revision,
  disabled,
  onSelect,
}: {
  inspectionId: string;
  revision: number;
  disabled: boolean;
  onSelect: (selection: RepairSelection) => void;
}) {
  const { history, busy, load } = useInspectionHistory(
    null,
    `reports/${inspectionId}/revisions/${revision}/repair-history`,
    parseRepairs,
    "Previous repair work could not be loaded. Retry without leaving your current draft.",
    { inspectionId, revision },
  );
  return (
    <section
      className={styles.paging}
      aria-label={`Previous repair work · revision ${revision}`}
      aria-busy={busy}
    >
      <h3>Previous repair work · revision {revision}</h3>
      {!history && (
        <Button
          size="compact"
          variant="secondary"
          disabled={disabled || busy}
          onClick={() => void load(1, 25)}
        >
          {busy ? "Loading previous work…" : "Retry previous work"}
        </Button>
      )}
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
              disabled={disabled || busy}
              onClick={() => onSelect({ requestId: request.operationId, revision })}
            >
              View work and signed findings
            </Button>
          </li>
        ))}
      </ul>
      {history && (
        <DataTablePagination
          page={{ ...history, total: history.totalCount }}
          count={history.items.length}
          disabled={disabled || busy}
          onPage={(page, size) => void load(page, size)}
        />
      )}
    </section>
  );
}
