"use client";

import { useRef, useState, type ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { StaffQueueLayout } from "./StaffQueueLayout";
import { StaffQueueTabs } from "./StaffQueueTabs";
import { StaffQueueFooter } from "./StaffQueueFooter";
import { StaffQueueSearch } from "./StaffQueueSearch";
import { StaffQueueVehicleRow } from "./StaffQueueVehicleRow";
import type { useStaffWorklist } from "@/hooks/use-staff-worklist";
import type { IntakeDrawer } from "@/hooks/use-intake-history";
import { intakeQueues, type IntakeQueue } from "@/lib/staff/contracts";
import { AppointmentStatus } from "./AppointmentStatus";
import styles from "./StaffQueueWorkspace.module.css";
import { AssistedIntakeAction } from "./AssistedIntakeAction";

const labels: Record<IntakeQueue, string> = {
  toClaim: "To claim",
  myWork: "My work",
  history: "History",
};
const countKeys = { toClaim: "toClaim", myWork: "myWork", history: "history" } as const;

export function IntakeAgentQueue({
  worklist,
  drawer,
  children,
  className,
  inert,
}: {
  worklist: ReturnType<typeof useStaffWorklist>;
  drawer: IntakeDrawer;
  children: ReactNode;
  className?: string;
  inert?: boolean;
}) {
  const [expanded, setExpanded] = useState(!drawer.id);
  const [lastDrawerId, setLastDrawerId] = useState(drawer.id);
  const list = useRef<HTMLDivElement | null>(null);
  const { active, snapshot, busy, ready, draft, setDraft } = worklist;
  const queue = active.filters.queue || "myWork";
  const selected = Boolean(drawer.id || drawer.pendingId);

  if (lastDrawerId !== drawer.id) {
    setLastDrawerId(drawer.id);
    setExpanded(!drawer.id);
  }

  async function load(nextQueue = queue, page = 1, pageSize = active.filters.pageSize) {
    const changedPage =
      nextQueue !== queue || page !== active.page || pageSize !== active.filters.pageSize;
    const success = await worklist.load({ ...active.filters, queue: nextQueue, pageSize }, page);
    if (success && changedPage) {
      list.current?.scrollTo({ top: 0 });
    }
  }

  return (
    <StaffQueueLayout
      id="intake-queue-content"
      label="Intake queues"
      title={labels[queue]}
      selected={selected}
      expanded={expanded}
      onExpandedChange={setExpanded}
      className={className}
      inert={inert}
      tabs={
        <StaffQueueTabs
          label="Intake queue tabs"
          layout="queue"
          countNoun="appointments"
          tabs={intakeQueues.map((item) => ({
            value: item,
            label: labels[item],
            count: snapshot?.counts?.[countKeys[item]],
          }))}
          value={queue}
          disabled={!ready || busy}
          onChange={(item) => void load(item)}
        />
      }
      search={
        <StaffQueueSearch
          label="Search intake queue"
          placeholder="Search registration, vehicle or customer"
          value={draft.search}
          busy={busy}
          disabled={!ready}
          onChange={(search) => setDraft((current) => ({ ...current, search }))}
          onSubmit={() => {
            if (ready && !busy) {
              void worklist.load({ ...active.filters, search: draft.search }, 1);
            }
          }}
          onClear={
            active.filters.search && !busy
              ? () => void worklist.load({ ...active.filters, search: "" }, 1)
              : undefined
          }
          action={
            <AssistedIntakeAction
              open={drawer.creating}
              onOpen={drawer.openCreate}
              onClose={drawer.closeCreate}
              disabled={!ready || busy}
              onBooked={() => void worklist.load(active.filters, active.page)}
            />
          }
        />
      }
      queue={
        <div className={styles.queueList}>
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
                <StaffQueueVehicleRow
                  key={row.id}
                  row={row}
                  status={<AppointmentStatus status={row.status} />}
                  selected={row.id === (drawer.pendingId || drawer.id)}
                  pending={row.id === drawer.pendingId}
                  onOpen={() => {
                    setExpanded(false);
                    drawer.open(row.id);
                  }}
                />
              ))}
            </ul>
          </div>
          {snapshot && (
            <StaffQueueFooter
              page={snapshot}
              count={snapshot.items.length}
              busy={busy}
              failed={false}
              disabled={!ready}
              onPage={(page, pageSize) => void load(queue, page, pageSize)}
              onRetry={() => void load(queue, active.page)}
            />
          )}
        </div>
      }
    >
      {children}
    </StaffQueueLayout>
  );
}
