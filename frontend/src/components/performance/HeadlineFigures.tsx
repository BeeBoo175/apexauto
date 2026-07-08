import { PerformanceSection, type SectionState } from "./PerformanceSection";
import { StatsSkeleton } from "./PerformanceSkeletons";
import { StatGrid, type Stat } from "./StatGrid";
import styles from "./Performance.module.css";

// The first section of every Performance tab and of Home: the few figures a manager reads before anything else, in
// one layout, with what they cover on the right of the heading.
export function HeadlineFigures({
  stats,
  covers,
  state,
}: {
  stats: Stat[];
  covers?: string;
  state: SectionState;
}) {
  return (
    <PerformanceSection
      title="Headline figures"
      aside={covers && <span className={styles.panelAside}>{covers}</span>}
      state={state}
      placeholder={<StatsSkeleton />}
    >
      <StatGrid stats={stats} />
    </PerformanceSection>
  );
}
