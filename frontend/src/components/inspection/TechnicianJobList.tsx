"use client";

import { useEffect, useRef } from "react";
import { Button } from "@/components/ui/Button";
import { SearchField } from "@/components/ui/SearchField";
import type { useTechnicianQueue } from "@/hooks/use-technician-queue";
import type { WorkshopRow } from "@/lib/inspection/workshop-contracts";
import { WorkshopStatus } from "./WorkshopStatus";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { formatCurrency } from "@/lib/currency-format";
import styles from "./TechnicianWorkspace.module.css";

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
  const continuation = useRef<HTMLDivElement | null>(null);
  const { snapshot, loadMore, busy, failed } = state;
  const activeId = pendingId || selectedId;
  useEffect(() => {
    const target = continuation.current;
    if (
      !target ||
      busy ||
      failed ||
      !snapshot?.nextCursor ||
      typeof IntersectionObserver === "undefined"
    ) {
      return;
    }
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((entry) => entry.isIntersecting)) {
        void loadMore();
      }
    });
    observer.observe(target);
    return () => observer.disconnect();
  }, [busy, failed, snapshot?.nextCursor, loadMore]);

  return (
    <section className={styles.list} aria-label={title} aria-busy={busy}>
      {(!snapshot || snapshot.total > 0 || state.search || state.draft) && (
        <form
          className={styles.search}
          onSubmit={(event) => {
            event.preventDefault();
            if (!busy) {
              void state.submit();
            }
          }}
        >
          <SearchField
            label={`Search ${title.toLowerCase()}`}
            placeholder="Search vehicle or registration"
            value={state.draft}
            disabled={!state.ready}
            busy={busy}
            onChange={state.setDraft}
            onClear={state.search && !busy ? () => void state.clear() : undefined}
          />
        </form>
      )}
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
      {(failed || snapshot?.nextCursor) && (
        <div className={styles.continuation} ref={continuation}>
          {failed && <p>Jobs could not be loaded. Confirmed work remains shown.</p>}
          <Button
            size="compact"
            variant="secondary"
            disabled={busy}
            onClick={() => void (failed ? state.retry() : loadMore())}
          >
            {failed ? "Retry worklist" : "Load more jobs"}
          </Button>
          <span role="status" aria-live="polite">
            {busy ? "Loading jobs…" : `${snapshot?.items.length ?? 0} jobs loaded`}
          </span>
        </div>
      )}
    </section>
  );
}
