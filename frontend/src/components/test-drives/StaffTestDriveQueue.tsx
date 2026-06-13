"use client";

import { useRef, useState, type ReactNode } from "react";
import { StaffQueueLayout } from "@/components/staff/StaffQueueLayout";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { StaffQueueFooter } from "@/components/staff/StaffQueueFooter";
import { StaffQueueSearch } from "@/components/staff/StaffQueueSearch";
import { StaffQueueVehicleRow } from "@/components/staff/StaffQueueVehicleRow";
import type { useStaffTestDriveList } from "@/hooks/use-staff-test-drive-list";
import { testDriveQueues } from "@/lib/test-drives/staff-query";
import { StaffTestDriveStatus } from "./StaffTestDriveStatus";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";

export function StaffTestDriveQueue({
  list,
  selectedId,
  pendingId,
  open,
  children,
}: {
  list: ReturnType<typeof useStaffTestDriveList>;
  selectedId: string;
  pendingId: string;
  open: (id: string) => void;
  children: ReactNode;
}) {
  const [expanded, setExpanded] = useState(!selectedId);
  const scroller = useRef<HTMLDivElement | null>(null);
  const { snapshot, active, busy, draft, setDraft } = list;
  const queue =
    testDriveQueues.find((item) => item.value === active.filters.queue) ?? testDriveQueues[1];
  return (
    <StaffQueueLayout
      id="test-drive-queue"
      label="Test drive queues"
      title={queue.label}
      selected={Boolean(selectedId || pendingId)}
      expanded={expanded}
      onExpandedChange={setExpanded}
      tabs={
        <StaffQueueTabs
          label="Test drive queue tabs"
          layout="queue"
          countNoun="test drives"
          tabs={testDriveQueues.map((item) => ({ ...item, count: snapshot.counts?.[item.count] }))}
          value={queue.value}
          disabled={busy}
          onChange={(value) => {
            void list.load({ ...active.filters, queue: value }, 1).then((ok) => {
              if (ok) {
                scroller.current?.scrollTo({ top: 0 });
              }
            });
          }}
        />
      }
      search={
        <StaffQueueSearch
          label="Search test drive queue"
          placeholder="Search reference, vehicle or customer"
          value={draft.search}
          busy={busy}
          onChange={(search) => setDraft((current) => ({ ...current, search }))}
          onSubmit={() => {
            if (!busy) {
              void list.load({ ...active.filters, search: draft.search.trim() }, 1);
            }
          }}
          onClear={
            active.filters.search && !busy
              ? () => void list.load({ ...active.filters, search: "" }, 1)
              : undefined
          }
        />
      }
      queue={
        <div className={styles.queueList}>
          <div
            className={styles.list}
            key={`${snapshot.page}:${snapshot.pageSize}:${active.filters.search}`}
            ref={scroller}
            aria-busy={busy}
          >
            {snapshot.items.length === 0 && (
              <p className={styles.empty}>No test drives in this queue match your search.</p>
            )}
            <ul className={styles.jobs}>
              {snapshot.items.map((row) => (
                <StaffQueueVehicleRow
                  key={row.id}
                  row={row}
                  status={<StaffTestDriveStatus status={row.status} />}
                  selected={row.id === (pendingId || selectedId)}
                  pending={row.id === pendingId}
                  onOpen={() => {
                    setExpanded(false);
                    open(row.id);
                  }}
                />
              ))}
            </ul>
          </div>
          <StaffQueueFooter
            page={snapshot}
            count={snapshot.items.length}
            busy={busy}
            failed={false}
            onPage={(page, pageSize) => void list.load({ ...active.filters, pageSize }, page)}
            onRetry={() => void list.load(active.filters, active.page)}
          />
        </div>
      }
    >
      {children}
    </StaffQueueLayout>
  );
}
