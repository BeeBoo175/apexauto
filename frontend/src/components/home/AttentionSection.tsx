import type { CSSProperties } from "react";
import { StaffLink } from "@/components/staff/StaffLink";
import dashboard from "@/components/staff/DashboardSection.module.css";
import { Icon } from "@/components/ui/Icon";
import type { IconName } from "@/lib/icons";
import { formatCurrency } from "@/lib/currency-format";
import type {
  PerformancePipeline,
  PerformanceStageWaiting,
  PerformanceWorkload,
} from "@/lib/performance/performance";
import type { ScheduledWork } from "@/lib/staff/work-calendar";
import styles from "./Home.module.css";

interface AttentionLine {
  key: string;
  count: number;
  text: string;
  href: string;
}

export interface AttentionGroup {
  key: string;
  title: string;
  icon: IconName;
  // What the tile header says when lines are waiting, when their counts do not add up to one figure.
  summary?: string;
  lines: AttentionLine[];
}

interface AttentionSources {
  pipeline: PerformancePipeline | null;
  stages: PerformanceStageWaiting | null;
  workload: PerformanceWorkload | null;
  schedule: ScheduledWork[] | null;
}

function plural(count: number, one: string, many: string) {
  return count === 1 ? one : many;
}

function groupOf(pipeline: PerformancePipeline, group: string) {
  return pipeline.groups.find((item) => item.group === group) ?? { count: 0, agreedTotal: 0 };
}

function stageCount(stages: PerformanceStageWaiting, stage: string) {
  return stages.stages.find((item) => item.stage === stage)?.inStageNow ?? 0;
}

function present(lines: (AttentionLine | false)[]) {
  return lines.filter((line): line is AttentionLine => line !== false && line.count > 0);
}

// Only counts the backend confirms, grouped by where the manager acts, each line opening the exact queue tab.
export function managerAttention({ pipeline, stages, workload, schedule }: AttentionSources) {
  const approval = pipeline ? groupOf(pipeline, "NeedsApproval") : { count: 0, agreedTotal: 0 };
  const unclaimed = pipeline ? groupOf(pipeline, "Unclaimed") : { count: 0, agreedTotal: 0 };
  const handover = pipeline
    ? groupOf(pipeline, "SettledUndelivered")
    : { count: 0, agreedTotal: 0 };
  const unassigned = schedule?.filter((item) => !item.handlerId).length ?? 0;
  const stretched = workload?.stretchedCount ?? 0;
  const groups: AttentionGroup[] = [
    {
      key: "sales",
      title: "Sales",
      icon: "car",
      lines: present([
        {
          key: "approval",
          count: approval.count,
          text: `to approve, ${formatCurrency(approval.agreedTotal)}`,
          href: "/dashboard/sales?view=needsApproval",
        },
        {
          key: "unclaimed",
          count: unclaimed.count,
          text: "not claimed by an agent",
          href: "/dashboard/sales?view=open",
        },
        {
          key: "handover",
          count: handover.count,
          text: `settled, ${plural(handover.count, "waits", "wait")} for handover`,
          href: "/dashboard/sales?view=awaitingHandover",
        },
      ]),
    },
    {
      key: "bookings",
      title: "Today's bookings",
      icon: "calendar",
      lines: present([
        {
          key: "unassigned",
          count: unassigned,
          text: "with no one assigned",
          href: "/dashboard/work-calendar",
        },
      ]),
    },
    {
      key: "stock",
      title: "Stock",
      icon: "inspection",
      lines: present([
        stages !== null && {
          key: "inspection",
          count: stageCount(stages, "AwaitingInspection"),
          text: "waiting for inspection",
          href: "/dashboard/inspection?view=inspection",
        },
        stages !== null && {
          key: "listing",
          count: stageCount(stages, "AwaitingListing"),
          text: "inspected, ready to list",
          href: "/dashboard/management",
        },
      ]),
    },
    {
      key: "people",
      title: "Work over a week old",
      icon: "users",
      summary: `${stretched} ${plural(stretched, "person", "people")}`,
      lines: present([
        {
          key: "stretched",
          count: stretched,
          text: `${plural(stretched, "person holds", "people hold")} work open over a week`,
          href: "/dashboard/work-calendar",
        },
      ]),
    },
  ];
  return groups;
}

// What needs the user now, as flat columns grouped by where they act, divided by hairlines like the rest of the
// staff workspace. Every column keeps the band's height whether it has lines or not, so the band never moves.
export function AttentionBand({
  groups,
  ready,
  failed,
}: {
  groups: AttentionGroup[];
  ready: boolean;
  failed: boolean;
}) {
  const waiting = groups.reduce(
    (sum, group) => sum + group.lines.reduce((lines, line) => lines + line.count, 0),
    0,
  );
  return (
    <section className={styles.band} aria-labelledby="home-attention">
      <div className={styles.bandHead}>
        <h2 id="home-attention" className={dashboard.panelTitle}>
          Needs you now
        </h2>
        {ready && waiting === 0 && (
          <span className={dashboard.panelAside}>Nothing waits on you</span>
        )}
      </div>
      {!ready && failed && (
        <p className={dashboard.note}>
          Some figures could not be loaded, so this may be incomplete. Refresh to try again.
        </p>
      )}
      <div className={styles.tiles} style={{ "--tiles": groups.length } as CSSProperties}>
        {groups.map((group) => (
          <div
            key={group.key}
            className={styles.tile}
            data-tone={tileTone(group, ready)}
            aria-busy={!ready}
          >
            <div className={styles.tileHead}>
              <span className={styles.tileIcon} aria-hidden="true">
                <Icon name={group.icon} />
              </span>
              <h3 className={styles.tileTitle}>{group.title}</h3>
            </div>
            {ready && <span className={styles.tileTotal}>{groupTotal(group)}</span>}
            <AttentionLines group={group} ready={ready} />
          </div>
        ))}
      </div>
    </section>
  );
}

// A column with lines waiting draws its counts in amber; a clear one stays quiet and says so in words.
function tileTone(group: AttentionGroup, ready: boolean) {
  if (!ready) {
    return "pending";
  }
  return group.lines.length > 0 ? "waiting" : "clear";
}

// The one figure that heads each column, in the same type as the headline figures on Performance.
function groupTotal(group: AttentionGroup) {
  return group.lines.reduce((sum, line) => sum + line.count, 0);
}

function AttentionLines({ group, ready }: { group: AttentionGroup; ready: boolean }) {
  if (!ready) {
    return <span className={`${dashboard.placeholder} ${styles.shellTile}`} />;
  }
  if (group.lines.length === 0) {
    return <p className={styles.allClear}>Nothing waiting</p>;
  }
  return (
    <ul className={styles.tileLines}>
      {group.lines.map((line) => (
        <li key={line.key}>
          <StaffLink href={line.href} className={styles.tileLine}>
            <span className={styles.tileCount}>{line.count}</span>
            <span className={styles.tileText}>{line.text}</span>
          </StaffLink>
        </li>
      ))}
    </ul>
  );
}
