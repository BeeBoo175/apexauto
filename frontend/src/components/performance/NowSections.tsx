import { stageLabels, type PerformanceStageWaiting } from "@/lib/performance/performance";
import { MeasureBars } from "./MeasureBars";
import { PerformanceSection, type SectionState } from "./PerformanceSection";
import { RowsSkeleton } from "./PerformanceSkeletons";
import styles from "./Performance.module.css";

function waitText(days: number | null, passes: number) {
  if (days === null || passes === 0) {
    return "No recent exits to time";
  }
  if (days < 1) {
    return "Usually under a day here";
  }
  const rounded = Math.round(days);
  return `Usually ${rounded} ${rounded === 1 ? "day" : "days"} here`;
}

const stageLinks: Record<string, string> = {
  AwaitingInspection: "/dashboard/inspection",
  InInspection: "/dashboard/inspection",
  InRepair: "/dashboard/inspection",
  AwaitingListing: "/dashboard/management",
  Listed: "/dashboard/management",
  SaleInProgress: "/dashboard/sales",
};

// Where vehicles stand today, stage by stage in lifecycle order. The usual wait comes from vehicles that left each
// stage in the last 90 days. Each stage opens the page that works on it.
export function StageNowSection({
  stages,
  state,
}: {
  stages: PerformanceStageWaiting | null;
  state: SectionState;
}) {
  const total = stages?.stages.reduce((sum, stage) => sum + stage.inStageNow, 0) ?? 0;
  return (
    <PerformanceSection
      title="Stock by stage"
      aside={<span className={styles.panelAside}>{total} vehicles</span>}
      state={{ ...state, hasData: stages !== null }}
      placeholder={<RowsSkeleton count={6} />}
    >
      <MeasureBars
        caption="Vehicles in each stage now, with the usual wait over the last 90 days"
        empty="No vehicles in stock."
        items={(stages?.stages ?? []).map((stage) => {
          const label = stageLabels[stage.stage] ?? stage.stage;
          return {
            key: stage.stage,
            label,
            detail: waitText(stage.averageDays, stage.completedCount),
            value: stage.inStageNow,
            text: String(stage.inStageNow),
            href: stageLinks[stage.stage],
            openLabel: "Open the page for this stage",
          };
        })}
      />
    </PerformanceSection>
  );
}
