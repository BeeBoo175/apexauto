"use client";

import { useState } from "react";
import { StaffLink } from "@/components/staff/StaffLink";
import { DashboardSection } from "@/components/staff/DashboardSection";
import { RowsSkeleton } from "@/components/staff/DashboardSkeletons";
import dashboard from "@/components/staff/DashboardSection.module.css";
import { useAgentHome } from "@/hooks/use-agent-home";
import { useTodaySchedule } from "@/hooks/use-today-schedule";
import type { CurrentActor } from "@/lib/auth/actor";
import type { HomeEntry } from "@/hooks/use-home-entry";
import type { SalesQueue } from "@/lib/sales/contracts";
import { queueViewQueries } from "@/lib/sales/contracts";
import { workItemHref, workItemLabel } from "@/lib/staff/work-calendar-presentation";
import type { ScheduledWork, UnscheduledWorkPage } from "@/lib/staff/work-calendar";
import { AttentionBand, type AttentionGroup } from "./AttentionSection";
import { HomeLayout } from "./HomeLayout";
import { ScheduleSection } from "./ScheduleSection";
import styles from "./Home.module.css";

const salesHref = (view: keyof typeof queueViewQueries) =>
  `/dashboard/sales?view=${queueViewQueries[view]}`;

// An agent's tiles: their sales queue and today's bookings, each line opening the exact queue or booking list.
function agentAttention(
  actor: CurrentActor,
  queue: SalesQueue | null,
  schedule: ScheduledWork[] | null,
): AttentionGroup[] {
  const counts = queue?.counts;
  const mine = schedule?.filter((item) => item.handlerId === actor.userId).length ?? 0;
  const unassigned = schedule?.filter((item) => !item.handlerId).length ?? 0;
  const present = (lines: AttentionGroup["lines"]) => lines.filter((line) => line.count > 0);
  return [
    {
      key: "claim",
      title: "To claim",
      icon: "user",
      lines: present([
        {
          key: "holds",
          count: counts?.toClaim ?? 0,
          text: "holds to claim",
          href: salesHref("ToClaim"),
        },
        {
          key: "bookings",
          count: unassigned,
          text: "bookings today with no one assigned",
          href: "/dashboard/work-calendar",
        },
      ]),
    },
    {
      key: "sales",
      title: "Your sales",
      icon: "car",
      lines: present([
        {
          key: "handling",
          count: counts?.handling ?? 0,
          text: "in progress",
          href: salesHref("Handling"),
        },
        {
          key: "waiting",
          count: counts?.waiting ?? 0,
          text: "awaiting manager approval",
          href: salesHref("Waiting"),
        },
        {
          key: "settling",
          count: counts?.settling ?? 0,
          text: "to settle or hand over",
          href: salesHref("Settling"),
        },
      ]),
    },
    {
      key: "today",
      title: "Your day",
      icon: "calendar",
      lines: present([
        {
          key: "mine",
          count: mine,
          text: "bookings assigned to you today",
          href: "/dashboard/work-calendar",
        },
      ]),
    },
  ];
}

// The agent's open work, the same items as the Work calendar's ongoing queue, each opening its record.
function OpenWorkSection({
  work,
  busy,
  failed,
  retry,
}: {
  work: UnscheduledWorkPage | null;
  busy: boolean;
  failed: boolean;
  retry: () => void;
}) {
  return (
    <DashboardSection
      title="Your open work"
      aside={
        work && (
          <span className={dashboard.panelAside}>
            {work.total} open,{" "}
            <StaffLink href="/dashboard/work-calendar">open the work calendar</StaffLink>
          </span>
        )
      }
      state={{ hasData: work !== null, busy, failed, retry }}
      placeholder={<RowsSkeleton count={5} />}
    >
      {work && work.items.length === 0 && <p className={dashboard.note}>You have no open work.</p>}
      {work && work.items.length > 0 && (
        <ul className={styles.workList}>
          {work.items.map((item) => (
            <li key={`${item.kind}:${item.id}`}>
              <StaffLink href={workItemHref(item)} className={styles.workItem}>
                <span className={styles.workPlate}>{item.registrationPlate}</span>
                <span className={styles.workVehicle}>{item.vehicle}</span>
                <span className={styles.workStatus}>{workItemLabel(item)}</span>
              </StaffLink>
            </li>
          ))}
        </ul>
      )}
    </DashboardSection>
  );
}

// A sales agent's Home: what to claim, their sales and their day, today's schedule and their open work.
export function AgentHome({ entry }: { entry: Extract<HomeEntry, { kind: "agent" }> }) {
  const { actor } = entry;
  const [refreshes, setRefreshes] = useState(0);
  const mine = useAgentHome(actor, entry.queue, entry.work, refreshes);
  const schedule = useTodaySchedule(actor, "", refreshes, entry.schedule);
  return (
    <HomeLayout
      busy={mine.busy || schedule.busy}
      toolbar={null}
      onRefresh={() => setRefreshes((value) => value + 1)}
      band={
        <AttentionBand
          groups={agentAttention(actor, mine.queue, schedule.data)}
          ready={mine.queue !== null && schedule.data !== null}
          failed={mine.failed || schedule.failed}
        />
      }
      left={
        <ScheduleSection
          items={schedule.data}
          state={{
            hasData: schedule.data !== null,
            busy: schedule.busy,
            failed: schedule.failed,
            retry: schedule.retry,
          }}
          showBranch={false}
        />
      }
      right={
        <OpenWorkSection
          work={mine.work}
          busy={mine.busy}
          failed={mine.failed}
          retry={mine.retry}
        />
      }
    />
  );
}
