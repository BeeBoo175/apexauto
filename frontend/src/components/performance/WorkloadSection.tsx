"use client";

import { useState } from "react";
import { PersonLabel } from "@/components/staff/PersonLabel";
import { StaffLink } from "@/components/staff/StaffLink";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { SearchField } from "@/components/ui/SearchField";
import { roleNames, type Role } from "@/lib/auth/actor";
import type { PerformanceWorkload, WorkloadPerson } from "@/lib/performance/performance";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import { RowsSkeleton } from "./PerformanceSkeletons";
import styles from "./Performance.module.css";

const ageBands = [
  { key: "under2Days", label: "Under 2 days", fill: styles.ageNew },
  { key: "twoToSevenDays", label: "2 to 7 days", fill: styles.ageMiddle },
  { key: "overSevenDays", label: "Over 7 days", fill: styles.ageOld },
] as const;

type Team = "sales" | "workshop";

const teamOf = (person: WorkloadPerson): Team =>
  person.role === "WorkshopTechnician" ? "workshop" : "sales";

// Open work per person today, aged from when the current holder received it: who is stretched and who has room.
// It lists only people holding open work, split by team, those with work open over a week first; within a group
// people stay in alphabetical order, so the grouping is by state and never a ranking. Each row opens that person's
// work on the Work calendar.
export function WorkloadSection({
  workload,
  state,
  showBranch,
}: {
  workload: PerformanceWorkload | null;
  state: SectionState;
  showBranch: boolean;
}) {
  const [team, setTeam] = useState<Team>("sales");
  const [search, setSearch] = useState("");
  const holding = (workload?.people ?? []).filter((person) => person.total > 0);
  const counts = {
    sales: holding.filter((person) => teamOf(person) === "sales").length,
    workshop: holding.filter((person) => teamOf(person) === "workshop").length,
  };
  const term = search.trim().toLowerCase();
  const shown = holding.filter(
    (person) =>
      teamOf(person) === team &&
      (!term || `${person.name} ${person.branchName}`.toLowerCase().includes(term)),
  );
  return (
    <PerformanceSection
      title="Who has room"
      aside={<AgeLegend />}
      tools={
        <div className={styles.roomTools}>
          <SearchField
            label="Search people"
            placeholder="Search people"
            value={search}
            disabled={false}
            onChange={setSearch}
            onClear={search ? () => setSearch("") : undefined}
          />
          <StaffQueueTabs
            label="Teams"
            countNoun="people with open work"
            tabs={[
              { value: "sales", label: "Sales & service", count: counts.sales },
              { value: "workshop", label: "Workshop", count: counts.workshop },
            ]}
            value={team}
            onChange={setTeam}
          />
        </div>
      }
      state={{ ...state, hasData: workload !== null }}
      placeholder={<RowsSkeleton count={5} tall />}
    >
      {workload && (
        <WorkloadGroups people={shown} showBranch={showBranch} searching={term !== ""} />
      )}
    </PerformanceSection>
  );
}

function AgeLegend() {
  return (
    <ul className={styles.legend} aria-label="Age of open work">
      {ageBands.map((band) => (
        <li key={band.key}>
          <span className={`${styles.swatch} ${band.fill}`} aria-hidden="true" />
          {band.label}
        </li>
      ))}
    </ul>
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
  // A general manager reading every branch sees people by branch first, then by name.
  const ordered = [...people].sort((first, second) =>
    showBranch
      ? first.branchName.localeCompare(second.branchName) || first.name.localeCompare(second.name)
      : first.name.localeCompare(second.name),
  );
  const groups = [
    {
      key: "stretched",
      title: "Work open over a week",
      people: ordered.filter((p) => p.overSevenDays > 0),
    },
    {
      key: "current",
      title: "All work under a week",
      people: ordered.filter((p) => p.overSevenDays === 0),
    },
  ].filter((group) => group.people.length > 0);
  const maximum = Math.max(1, ...people.map((person) => person.total));
  return (
    <div className={styles.roomGroups}>
      {groups.map((group) => (
        <section key={group.key} aria-label={group.title}>
          <h3 className={styles.groupTitle}>
            {group.title}, {group.people.length}
          </h3>
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
      <span className={`${styles.track} ${styles.ageTrack}`} aria-hidden="true">
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
