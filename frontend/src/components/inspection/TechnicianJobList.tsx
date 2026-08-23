"use client";

import { EmptyNotice } from "@/components/ui/EmptyNotice";
import { StaffQueueFooter } from "@/components/staff/StaffQueueFooter";
import { Button } from "@/components/ui/Button";
import type { useTechnicianQueue } from "@/hooks/use-technician-queue";
import type { IconName } from "@/lib/icons";
import type { TechnicianTask, WorkshopRow } from "@/lib/inspection/workshop-contracts";
import { WorkshopStatus } from "./WorkshopStatus";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { formatCurrency } from "@/lib/currency-format";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";

const emptyTabs: Record<TechnicianTask, { icon: IconName; title: string; text: string }> = {
  MyWork: {
    icon: "tool",
    title: "Nothing ready to work",
    text: "Claim a vehicle from To claim to start.",
  },
  Waiting: {
    icon: "clock",
    title: "Nothing awaiting approval",
    text: "Repairs sent for approval appear here.",
  },
  Claimable: {
    icon: "car",
    title: "No vehicles to claim",
    text: "Vehicles ready for inspection appear here.",
  },
};

export function TechnicianJobList({
  title,
  view,
  state,
  onOpen,
  claim = false,
  selectedId = "",
  pendingId = "",
  selectionLocked = false,
  onGoToClaim,
}: {
  title: string;
  view: TechnicianTask;
  state: ReturnType<typeof useTechnicianQueue>;
  onOpen: (row: WorkshopRow) => void;
  claim?: boolean;
  selectedId?: string;
  pendingId?: string;
  selectionLocked?: boolean;
  // Opens the To claim tab, which the empty Ready tab points to.
  onGoToClaim?: () => void;
}) {
  const { snapshot, busy, failed } = state;
  const activeId = pendingId || selectedId;
  const empty = emptyTabs[view];

  return (
    <section className={styles.queueList} aria-label={title} aria-busy={busy}>
      <div className={styles.list} key={`${snapshot?.page}:${snapshot?.pageSize}:${state.search}`}>
        {!snapshot && !failed && !busy && (
          <EmptyNotice icon="alert-triangle" tone="alert" title="Worklist unavailable">
            Reload the page to try again.
          </EmptyNotice>
        )}
        {snapshot?.items.length === 0 && state.search && (
          <EmptyNotice
            icon="search"
            title="No matches"
            action={{ label: "Clear search", disabled: busy, onClick: () => void state.clear() }}
          >
            No job matches this search.
          </EmptyNotice>
        )}
        {snapshot?.items.length === 0 && !state.search && (
          <EmptyNotice
            icon={empty.icon}
            title={empty.title}
            link={
              view === "MyWork" && onGoToClaim
                ? { text: "To claim", onClick: onGoToClaim }
                : undefined
            }
          >
            {empty.text}
          </EmptyNotice>
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
      />
    </section>
  );
}
