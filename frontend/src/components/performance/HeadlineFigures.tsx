import { DashboardSection, type SectionState } from "@/components/staff/DashboardSection";
import { StatsSkeleton } from "./PerformanceSkeletons";
import { StatGrid, type Stat } from "./StatGrid";

// The first section of every Performance tab and of Home: the few figures a manager reads before anything else, in
// one layout. The period they cover is on the filter bar above.
export function HeadlineFigures({ stats, state }: { stats: Stat[]; state: SectionState }) {
  return (
    <DashboardSection title="Summary" state={state} placeholder={<StatsSkeleton />}>
      <StatGrid stats={stats} />
    </DashboardSection>
  );
}
