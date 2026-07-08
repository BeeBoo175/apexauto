"use client";

import { useEffect, useState } from "react";
import { PersonLabel } from "@/components/staff/PersonLabel";
import { StaffLink } from "@/components/staff/StaffLink";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { SearchField } from "@/components/ui/SearchField";
import { DataTablePagination } from "@/components/ui/data-table/DataTablePagination";
import { roleNames, type Role } from "@/lib/auth/actor";
import type { PerformanceWorkload, WorkloadPerson } from "@/lib/performance/performance";
import type { RoomView } from "@/lib/performance/room";
import { Legend } from "./Legend";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import { RowsSkeleton } from "./PerformanceSkeletons";
import styles from "./Performance.module.css";

const ageBands = [
  { key: "under2Days", label: "Under 2 days", fill: styles.ageNew },
  { key: "twoToSevenDays", label: "2 to 7 days", fill: styles.ageMiddle },
  { key: "overSevenDays", label: "Over 7 days", fill: styles.ageOld },
] as const;

const searchDelay = 300;

// Open work per person today, aged from when the current holder received it: who is stretched and who has room.
// It lists only people holding open work, one page of a team at a time, those with work open over a week first;
// within a group people stay in alphabetical order, so the grouping is by state and never a ranking and a page
// boundary never depends on a measure. Each row opens that person's work on the Work calendar.
export function WorkloadSection({
  workload,
  state,
  showBranch,
  view,
  onView,
}: {
  workload: PerformanceWorkload | null;
  state: SectionState;
  showBranch: boolean;
  view: RoomView;
  onView: (next: RoomView) => void;
}) {
  const [draft, setDraft] = useState(view.search);
  useEffect(() => {
    if (draft.trim() === view.search.trim()) {
      return;
    }
    const timer = setTimeout(() => onView({ ...view, search: draft, page: 1 }), searchDelay);
    return () => clearTimeout(timer);
  }, [draft, view, onView]);
  const searching = view.search.trim() !== "";
  return (
    <PerformanceSection
      title="Who has room"
      aside={<AgeLegend />}
      tools={
        <div className={styles.roomTools}>
          <SearchField
            label="Search people"
            placeholder="Search people"
            value={draft}
            disabled={false}
            onChange={setDraft}
            onClear={draft ? () => setDraft("") : undefined}
          />
          <StaffQueueTabs
            label="Teams"
            countNoun="people with open work"
            layout="queue"
            tabs={[
              { value: "sales", label: "Sales & service", count: workload?.salesCount },
              { value: "workshop", label: "Workshop", count: workload?.workshopCount },
            ]}
            value={view.team}
            onChange={(team) => onView({ ...view, team, page: 1 })}
          />
        </div>
      }
      footer={
        workload && (
          <DataTablePagination
            page={{ page: workload.page, pageSize: workload.pageSize, total: workload.total }}
            count={workload.people.length}
            disabled={state.busy}
            onPage={(page, pageSize) =>
              onView({ ...view, page: pageSize === view.pageSize ? page : 1, pageSize })
            }
          />
        )
      }
      state={{ ...state, hasData: workload !== null }}
      placeholder={<RowsSkeleton count={5} tall />}
    >
      {workload && (
        <WorkloadGroups people={workload.people} showBranch={showBranch} searching={searching} />
      )}
    </PerformanceSection>
  );
}

function AgeLegend() {
  return (
    <Legend
      label="Age of open work"
      items={ageBands.map((band) => ({ key: band.key, label: band.label, className: band.fill }))}
    />
  );
}

function WorkloadGroups({
  people,
  showBranch,
  searching,
}: {
  people: WorkloadPerson[];
  showBranch: boolean;
  searching: boolean;
}) {
  if (people.length === 0) {
    return (
      <p className={styles.note}>
        {searching ? "No one matches that search." : "No one in this team holds open work."}
      </p>
    );
  }
  // The read already orders people: work over a week first, then by branch for a general manager, then by name.
  const groups = [
    {
      key: "stretched",
      title: "Work open over a week",
      people: people.filter((p) => p.overSevenDays > 0),
    },
    {
      key: "current",
      title: "All work under a week",
      people: people.filter((p) => p.overSevenDays === 0),
    },
  ].filter((group) => group.people.length > 0);
  const maximum = Math.max(1, ...people.map((person) => person.total));
  return (
    <div className={styles.roomGroups}>
      {groups.map((group) => (
        <section key={group.key} aria-label={group.title}>
          <h3 className={styles.groupTitle}>{group.title}</h3>
          <ul className={styles.people}>
            {group.people.map((person) => (
              <li key={person.id}>
                <WorkloadRow person={person} maximum={maximum} showBranch={showBranch} />
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

// The one age figure a manager acts on; the bar and the row's accessible name carry the other two bands.
function overWeek(person: WorkloadPerson) {
  return person.overSevenDays > 0 ? `${person.overSevenDays} over a week` : "Nothing over a week";
}

function WorkloadRow({
  person,
  maximum,
  showBranch,
}: {
  person: WorkloadPerson;
  maximum: number;
  showBranch: boolean;
}) {
  const role = roleNames[person.role as Role] ?? person.role;
  const age =
    person.overSevenDays > 0
      ? `${person.overSevenDays} over a week, ${person.twoToSevenDays} at 2 to 7 days, ${person.under2Days} under 2`
      : `${person.twoToSevenDays} at 2 to 7 days, ${person.under2Days} under 2 days`;
  return (
    <StaffLink
      href={`/dashboard/work-calendar?search=${encodeURIComponent(person.name)}`}
      className={styles.personRow}
      aria-label={`${person.name}, ${person.total} open: ${age}. Open their work on the calendar`}
    >
      <span className={styles.personCell}>
        <PersonLabel
          id={person.id}
          name={person.name}
          identity={showBranch ? `${role}, ${person.branchName}` : role}
          pictureId={person.pictureId}
          nested
        />
        <span className={styles.personMeta}>
          {showBranch ? `${person.branchName}, ${overWeek(person)}` : overWeek(person)}
        </span>
      </span>
      <span
        className={`${styles.track} ${styles.ageTrack} ${styles.tipHost}`}
        data-tip={`${person.under2Days} under 2 days, ${person.twoToSevenDays} at 2 to 7 days, ${person.overSevenDays} over 7 days`}
        aria-hidden="true"
      >
        {ageBands.map((band) => (
          <span
            key={band.key}
            className={`${styles.fill} ${band.fill}`}
            style={{ width: `${(person[band.key] / maximum) * 100}%` }}
          />
        ))}
      </span>
      <span className={styles.personCount}>
        {person.total}
        <span className={styles.personUnit}> open</span>
      </span>
    </StaffLink>
  );
}
