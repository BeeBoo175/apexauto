"use client";

import { useRef, useState } from "react";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { SearchField } from "@/components/ui/SearchField";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import type { useStaffWorklist } from "@/hooks/use-staff-worklist";
import type { useStaffAppointmentDrawer } from "@/hooks/use-staff-appointment-drawer";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { intakeQueues, type IntakeQueue } from "@/lib/staff/contracts";
import { AppointmentStatus } from "./AppointmentStatus";
import styles from "./StaffQueueWorkspace.module.css";

const labels: Record<IntakeQueue, string> = {
  ToClaim: "To claim",
  MyWork: "My work",
  History: "History",
};
const countKeys = { ToClaim: "toClaim", MyWork: "myWork", History: "history" } as const;

export function IntakeAgentQueue({
  worklist,
  drawer,
}: {
  worklist: ReturnType<typeof useStaffWorklist>;
  drawer: ReturnType<typeof useStaffAppointmentDrawer>;
}) {
  const [expanded, setExpanded] = useState(!drawer.id);
  const list = useRef<HTMLDivElement | null>(null);
  const { active, snapshot, busy, ready, draft, setDraft } = worklist;
  const queue = active.filters.queue || "ToClaim";
  const selected = Boolean(drawer.id || drawer.pendingId);

  async function load(nextQueue = queue, page = 1, pageSize = active.filters.pageSize) {
    const changedPage =
      nextQueue !== queue || page !== active.page || pageSize !== active.filters.pageSize;
    const success = await worklist.load({ ...active.filters, queue: nextQueue, pageSize }, page);
    if (success && changedPage) {
      list.current?.scrollTo({ top: 0 });
    }
  }

  return (
    <aside className={styles.queue} aria-label="Intake queues" data-expanded={expanded}>
      {selected && (
        <div className={styles.mobileQueueBar}>
          <strong>{labels[queue]}</strong>
          <Button
            size="compact"
            variant="secondary"
            aria-expanded={expanded}
            aria-controls="intake-queue-content"
            onClick={() => setExpanded(!expanded)}
          >
            {expanded ? "Hide queue" : "Show queue"}
          </Button>
        </div>
      )}
      <div className={styles.queueBody} id="intake-queue-content">
        <nav className={styles.queueNavigation} aria-label="Intake queue tabs">
          {intakeQueues.map((item) => (
            <Button
              key={item}
              size="compact"
              variant="ghost"
              className={styles.queueView}
              aria-pressed={queue === item}
              disabled={!ready || busy}
              onClick={() => void load(item)}
            >
              <span>{labels[item]}</span>
              {snapshot?.counts && (
                <Badge variant="count" data-selected={queue === item}>
                  {snapshot.counts[countKeys[item]]}
                  <span className="sr-only"> appointments</span>
                </Badge>
              )}
            </Button>
          ))}
        </nav>
        <form
          className={styles.search}
          onSubmit={(event) => {
            event.preventDefault();
            if (ready && !busy) {
              void worklist.load({ ...active.filters, search: draft.search }, 1);
            }
          }}
        >
          <SearchField
            label="Search intake queue"
            placeholder="Search registration, vehicle or customer"
            value={draft.search}
            disabled={!ready}
            busy={busy}
            onChange={(search) => setDraft((current) => ({ ...current, search }))}
            onClear={
              active.filters.search && !busy
                ? () => void worklist.load({ ...active.filters, search: "" }, 1)
                : undefined
            }
          />
        </form>
        <div className={styles.list} ref={list} aria-busy={busy}>
          {!snapshot && (
            <div className={styles.empty}>
              <Button
                size="compact"
                variant="secondary"
                disabled={!ready || busy}
                onClick={() => void load(queue, active.page)}
              >
                Retry queue
              </Button>
            </div>
          )}
          {snapshot?.items.length === 0 && (
            <p className={styles.empty}>No appointments in this queue match your search.</p>
          )}
          <ul className={styles.jobs}>
            {snapshot?.items.map((row) => (
              <li key={row.id}>
                <Button
                  variant="ghost"
                  size="row"
                  className={styles.job}
                  aria-pressed={row.id === (drawer.pendingId || drawer.id)}
                  aria-busy={row.id === drawer.pendingId || undefined}
                  onClick={() => {
                    setExpanded(false);
                    drawer.open(row.id);
                  }}
                >
                  <span className={styles.jobHeading}>
                    <span className={styles.identity}>
                      <strong>{row.registrationPlate}</strong>
                      <span>
                        {row.manufactureYear} {row.make} {row.model} {row.trim}
                      </span>
                    </span>
                    <AppointmentStatus status={row.status} />
                  </span>
                  <span className={styles.jobMeta}>
                    {row.customerName} · {formatMalaysianDateTime(row.startsAt)}
                  </span>
                </Button>
              </li>
            ))}
          </ul>
        </div>
        {snapshot && (
          <DataTablePagination
            page={snapshot}
            count={snapshot.items.length}
            disabled={!ready || busy}
            onPage={(page, pageSize) => void load(queue, page, pageSize)}
          />
        )}
      </div>
    </aside>
  );
}
