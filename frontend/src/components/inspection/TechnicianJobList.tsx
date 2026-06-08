"use client";

import { StaffQueueFooter } from "@/components/staff/StaffQueueFooter";
import { Button } from "@/components/ui/Button";
import type { useTechnicianQueue } from "@/hooks/use-technician-queue";
import type { WorkshopRow } from "@/lib/inspection/workshop-contracts";
import { WorkshopStatus } from "./WorkshopStatus";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { formatCurrency } from "@/lib/currency-format";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";

export function TechnicianJobList({
  title,
  state,
  onOpen,
  claim = false,
  selectedId = "",
  pendingId = "",
  selectionLocked = false,
}: {
  title: string;
  state: ReturnType<typeof useTechnicianQueue>;
  onOpen: (row: WorkshopRow) => void;
  claim?: boolean;
  selectedId?: string;
  pendingId?: string;
  selectionLocked?: boolean;
}) {
  const { snapshot, busy, failed } = state;
  const activeId = pendingId || selectedId;

  return (
    <section className={styles.queueList} aria-label={title} aria-busy={busy}>
      <div className={styles.list} key={`${snapshot?.page}:${snapshot?.pageSize}:${state.search}`}>
        {!snapshot && !failed && (
          <div className={styles.empty}>
            <p>This worklist is unavailable.</p>
            <Button
              size="compact"
              variant="secondary"
              disabled={busy}
              onClick={() => void state.retry()}
            >
              Retry worklist
            </Button>
          </div>
        )}
        {snapshot?.items.length === 0 && (
          <p className={styles.empty}>
            {state.search ? "No jobs match this search." : "No jobs in this worklist."}
          </p>
        )}
        <ul className={styles.jobs}>
          {snapshot?.items.map((row) => (
            <li key={row.vehicleId}>
              <Button
                variant="ghost"
                size="row"
                className={styles.job}
                onClick={() => onOpen(row)}
                aria-pressed={!claim && row.inspectionId === activeId}
                aria-busy={(!claim && row.inspectionId === pendingId) || undefined}
                disabled={selectionLocked && (claim || row.inspectionId !== selectedId)}
                aria-label={`${claim ? "Claim inspection" : "Open job"} · ${row.registrationPlate}`}
              >
                <span className={styles.jobHeading}>
                  <span className={styles.identity}>
                    <strong>{row.registrationPlate}</strong>
                    <span>
                      {row.make} {row.model}
                    </span>
                  </span>
                  <WorkshopStatus status={row.status} />
                </span>
                <span className={styles.jobState}>
                  {claim && <strong>Claim inspection</strong>}
                  <span className={styles.jobMeta}>
                    {row.dateSource === "Requested" ? "Requested" : "Checked in"}{" "}
                    {formatMalaysianDateTime(row.since)}
                    {row.budget !== null && ` · ${formatCurrency(row.budget)}`}
                  </span>
                </span>
              </Button>
            </li>
          ))}
        </ul>
      </div>
      <StaffQueueFooter
        page={snapshot ?? null}
        count={snapshot?.items.length ?? 0}
        busy={busy}
        failed={failed}
        onPage={(page, pageSize) => void state.changePage(page, pageSize)}
        onRetry={() => void state.retry()}
      />
    </section>
  );
}
