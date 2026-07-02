import { Avatar } from "@/components/ui/Avatar";
import { roleNames, type Role } from "@/lib/auth/actor";
import type { PerformanceWorkload } from "@/lib/performance/performance";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import styles from "./Performance.module.css";

const ageBands = [
  { key: "under2Days", label: "Under 2 days", fill: styles.fillTrust },
  { key: "twoToSevenDays", label: "2 to 7 days", fill: styles.fillWarning },
  { key: "overSevenDays", label: "Over 7 days", fill: styles.fillSignal },
] as const;

export function WorkloadSection({
  workload,
  state,
  asTable,
}: {
  workload: PerformanceWorkload | null;
  state: SectionState;
  asTable: boolean;
}) {
  return (
    <PerformanceSection title="Who can take more work" now state={state}>
      {workload && (
        <>
          {asTable ? <WorkloadTable workload={workload} /> : <WorkloadBars workload={workload} />}
          <ul className={styles.legend}>
            {ageBands.map((band) => (
              <li key={band.key}>
                <span className={styles.swatch} style={{ background: bandColor(band.key) }} />
                {band.label}
              </li>
            ))}
            <li>Age runs from the current holder&apos;s assignment</li>
          </ul>
        </>
      )}
    </PerformanceSection>
  );
}

function bandColor(key: (typeof ageBands)[number]["key"]) {
  if (key === "under2Days") {
    return "var(--color-trust)";
  }
  return key === "twoToSevenDays" ? "var(--color-warning)" : "var(--color-signal)";
}

function PersonCell({ person }: { person: PerformanceWorkload["people"][number] }) {
  return (
    <span className={styles.person}>
      <Avatar
        accountId={person.id}
        name={person.name}
        pictureId={null}
        size="sm"
        detail={roleNames[person.role as Role] ?? person.role}
        label={person.name}
      />
      <span>{person.name}</span>
    </span>
  );
}

function WorkloadBars({ workload }: { workload: PerformanceWorkload }) {
  const maximum = Math.max(1, ...workload.people.map((person) => person.total));
  return (
    <ul className={styles.bars} aria-label="Open work per person, by age">
      {workload.people.map((person) => (
        <li key={`${person.id}`} className={styles.barRow}>
          <PersonCell person={person} />
          <span className={styles.track} aria-hidden="true">
            {ageBands.map((band) => (
              <span
                key={band.key}
                className={`${styles.fill} ${band.fill}`}
                style={{ width: `${(person[band.key] / maximum) * 100}%` }}
              />
            ))}
          </span>
          <span className={styles.barValue}>
            {person.total}
            <span className="sr-only">
              , {person.under2Days} under 2 days, {person.twoToSevenDays} from 2 to 7 days,{" "}
              {person.overSevenDays} over 7 days
            </span>
          </span>
        </li>
      ))}
    </ul>
  );
}

function WorkloadTable({ workload }: { workload: PerformanceWorkload }) {
  return (
    <div className={styles.scroll}>
      <table className={styles.table}>
        <caption className="sr-only">Open work per person, by age</caption>
        <thead>
          <tr>
            <th scope="col">Person</th>
            <th scope="col">Branch</th>
            <th scope="col" className={styles.number}>
              Under 2 days
            </th>
            <th scope="col" className={styles.number}>
              2 to 7 days
            </th>
            <th scope="col" className={styles.number}>
              Over 7 days
            </th>
          </tr>
        </thead>
        <tbody>
          {workload.people.map((person) => (
            <tr key={person.id}>
              <th scope="row">{person.name}</th>
              <td>{person.branchName}</td>
              <td className={styles.number}>{person.under2Days}</td>
              <td className={styles.number}>{person.twoToSevenDays}</td>
              <td className={styles.number}>{person.overSevenDays}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
