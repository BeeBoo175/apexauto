import { formatCurrency } from "@/lib/currency-format";
import { pipelineLabels, type PerformancePipeline } from "@/lib/performance/performance";
import { PerformanceBars } from "./PerformanceBars";
import { PerformanceSection, type SectionState } from "./PerformanceSection";

export function PipelineSection({
  pipeline,
  state,
  asTable,
}: {
  pipeline: PerformancePipeline | null;
  state: SectionState;
  asTable: boolean;
}) {
  return (
    <PerformanceSection title="Sales pipeline" now half state={state}>
      {pipeline && (
        <PerformanceBars
          rows={pipeline.groups.map((group) => ({
            key: group.group,
            label: pipelineLabels[group.group] ?? group.group,
            value: group.count,
            valueText: String(group.count),
            detail: `${formatCurrency(group.agreedTotal)} agreed`,
          }))}
          asTable={asTable}
          caption="Sales by pipeline group, with agreed totals"
          valueHeading="Sales"
          detailHeading="Agreed total"
        />
      )}
    </PerformanceSection>
  );
}
