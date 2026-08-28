"use client";

import { useRef, type ReactNode } from "react";
import { EmptyNotice } from "@/components/ui/EmptyNotice";
import { StaffQueueLayout } from "@/components/staff/StaffQueueLayout";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { StaffQueueFooter } from "@/components/staff/StaffQueueFooter";
import { StaffQueueSearch } from "@/components/staff/StaffQueueSearch";
import { StaffQueueVehicleRow } from "@/components/staff/StaffQueueVehicleRow";
import type { useStaffTestDriveList } from "@/hooks/use-staff-test-drive-list";
import type { IconName } from "@/lib/icons";
import { testDriveQueues } from "@/lib/test-drives/staff-query";
import { StaffTestDriveStatus } from "./StaffTestDriveStatus";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";
import { AssistedVehicleAction } from "@/components/staff/AssistedVehicleAction";

const emptyQueues: Record<
  (typeof testDriveQueues)[number]["value"],
  { icon: IconName; title: string; text: string }
> = {
  toClaim: {
    icon: "calendar",
    title: "No test drives to claim",
    text: "New bookings appear here to claim.",
  },
  myWork: {
    icon: "car",
    title: "Nothing in progress",
    text: "Claim one from To claim to start.",
  },
  history: {
    icon: "clock",
    title: "No history yet",
    text: "Finished test drives appear here.",
  },
};

export function StaffTestDriveQueue({
  list,
  selectedId,
  pendingId,
  open,
  onClose,
  children,
}: {
  list: ReturnType<typeof useStaffTestDriveList>;
  selectedId: string;
  pendingId: string;
  open: (id: string) => void;
  onClose?: () => void;
  children: ReactNode;
}) {
  const scroller = useRef<HTMLDivElement | null>(null);
  const { snapshot, active, busy, draft, setDraft } = list;
  const queue =
    testDriveQueues.find((item) => item.value === active.filters.queue) ?? testDriveQueues[1];

  function clearSearch() {
    void list.load({ ...active.filters, search: "" }, 1);
  }

  return (
    <StaffQueueLayout
      id="test-drive-queue"
      label="Test drive queues"
      selected={Boolean(selectedId || pendingId)}
      onBackToQueue={onClose}
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
          onClear={active.filters.search && !busy ? clearSearch : undefined}
          action={
            <AssistedVehicleAction
              kind="testDrive"
              disabled={busy}
              onClosed={() => void list.load(active.filters, active.page)}
            />
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
            {snapshot.items.length === 0 && active.filters.search && (
              <EmptyNotice
                icon="search"
                title="No matches"
                action={{ label: "Clear search", disabled: busy, onClick: clearSearch }}
              >
                No test drive matches this search.
              </EmptyNotice>
            )}
            {snapshot.items.length === 0 && !active.filters.search && (
              <EmptyNotice
                icon={emptyQueues[queue.value].icon}
                title={emptyQueues[queue.value].title}
                link={
                  queue.value === "myWork"
                    ? {
                        text: "To claim",
                        onClick: () => void list.load({ ...active.filters, queue: "toClaim" }, 1),
                      }
                    : undefined
                }
              >
                {emptyQueues[queue.value].text}
              </EmptyNotice>
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
          />
        </div>
      }
    >
      {children}
    </StaffQueueLayout>
  );
}
