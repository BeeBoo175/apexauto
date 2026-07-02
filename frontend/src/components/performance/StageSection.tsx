import { formatDays } from "@/lib/performance/format";
import { stageLabels, type PerformanceStageWaiting } from "@/lib/performance/performance";
import { PerformanceBars } from "./PerformanceBars";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import styles from "./Performance.module.css";

export function StageSection({
  stages,
  state,
  asTable,
}: {
  stages: PerformanceStageWaiting | null;
  state: SectionState;
  asTable: boolean;
}) {
  return (
    <PerformanceSection title="Where stock is waiting" half state={state}>
      {stages && (
        <>
          <PerformanceBars
            rows={stages.stages.map((stage) => ({
              key: stage.stage,
              label: stageLabels[stage.stage] ?? stage.stage,
              value: stage.inStageNow,
              valueText: `${stage.inStageNow} now`,
              detail: `${formatDays(stage.averageDays)} on average over ${stage.completedCount} completed`,
            }))}
            asTable={asTable}
            caption="Vehicles in each stage now, with average elapsed days this period"
            valueHeading="In stage now"
            detailHeading="Average elapsed this period"
          />
          <p className={styles.note}>
            Counts are today. Averages are elapsed calendar days for each vehicle&apos;s first pass
            out of the stage during the period.
          </p>
        </>
      )}
    </PerformanceSection>
  );
}
