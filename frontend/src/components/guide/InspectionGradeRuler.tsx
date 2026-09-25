"use client";

import Link from "next/link";
import { useState, type CSSProperties } from "react";
import { buttonClassName } from "@/components/ui/Button";
import { formatCurrency } from "@/lib/currency-format";
import type { InspectionGradingPolicy } from "@/lib/guide/inspection-points";
import { evaluateInspectionOutcome } from "@/lib/guide/inspection-rules";
import type { GuideWarranty } from "@/lib/guide/purchase-guide";
import { isWarrantyPlanEligible, splitWarrantyName } from "@/lib/guide/warranty-plans";
import { SegmentedControl } from "@/components/ui/SegmentedControl";
import { label, severities, type Severity } from "@/lib/inspection/contracts";
import styles from "./InspectionGradeRuler.module.css";

const SLIDER_STEPS = 400;
const ZONE_COUNT = 4;
const STEPS_PER_ZONE = SLIDER_STEPS / ZONE_COUNT;

// Four equal zones on the slider, each mapped across one real cost band. The last zone runs to twice the cap.
function costAtStep(step: number, policy: InspectionGradingPolicy) {
  const edges = [
    0,
    policy.gradeBMinimumRepairCost,
    policy.gradeCMinimumRepairCost,
    policy.maximumRepairCost,
    policy.maximumRepairCost * 2,
  ];
  const zone = Math.min(Math.floor(step / STEPS_PER_ZONE), ZONE_COUNT - 1);
  const progress = (step - zone * STEPS_PER_ZONE) / STEPS_PER_ZONE;
  return Math.round(edges[zone] + (edges[zone + 1] - edges[zone]) * progress);
}

// Two inputs and the grade they give: how much repair the car needed, and the worst thing found. The cost track is
// drawn in the four grade bands, so the thumb is always seen sitting in the band it lands in.
export function InspectionGradeRuler({
  policy,
  warranties,
}: {
  policy: InspectionGradingPolicy;
  warranties: readonly GuideWarranty[];
}) {
  const [step, setStep] = useState(0);
  const [severity, setSeverity] = useState<Severity>("Advisory");

  const cost = costAtStep(step, policy);
  const outcome = evaluateInspectionOutcome(cost, severity, policy);
  const zones = [
    { key: "A", name: "A", range: `Under ${formatCurrency(policy.gradeBMinimumRepairCost)}` },
    {
      key: "B",
      name: "B",
      range: `${formatCurrency(policy.gradeBMinimumRepairCost)} to ${formatCurrency(policy.gradeCMinimumRepairCost - 0.01)}`,
    },
    {
      key: "C",
      name: "C",
      range: `${formatCurrency(policy.gradeCMinimumRepairCost)} to ${formatCurrency(policy.maximumRepairCost)}`,
    },
    { key: "X", name: "Not sold", range: `Over ${formatCurrency(policy.maximumRepairCost)}` },
  ];
  const rejected = outcome?.isRejected ?? false;
  const activeKey = rejected ? "X" : (outcome?.grade ?? "A");
  const letter = rejected ? "–" : (outcome?.grade ?? "A");
  const plans = warranties.map((plan) => ({
    id: plan.id,
    ...splitWarrantyName(plan.name),
    eligible: isWarrantyPlanEligible(plan.minimumGrade, outcome?.grade ?? null),
  }));

  return (
    <div className={styles.grader}>
      <div className={styles.controls}>
        <div className={styles.control}>
          <div className={styles.readout}>
            <label htmlFor="grade-ruler-cost">Repairs the car needed</label>
            <output htmlFor="grade-ruler-cost">{formatCurrency(cost)}</output>
          </div>
          <div
            className={styles.track}
            style={{ "--position": `${(step / SLIDER_STEPS) * 100}%` } as CSSProperties}
          >
            <div className={styles.bands} aria-hidden="true">
              {zones.map((zone) => (
                <div key={zone.key} data-zone={zone.key} data-active={zone.key === activeKey}>
                  <i />
                  <b>{zone.name}</b>
                  <span>{zone.range}</span>
                </div>
              ))}
            </div>
            <p className={styles.range} aria-hidden="true">
              {zones.find((zone) => zone.key === activeKey)?.range}
            </p>
            <input
              id="grade-ruler-cost"
              type="range"
              min={0}
              max={SLIDER_STEPS}
              value={step}
              className={styles.slider}
              onChange={(event) => setStep(Number(event.target.value))}
            />
          </div>
        </div>

        <div className={styles.control}>
          <span className={styles.controlLabel}>Worst finding on the sheet</span>
          <SegmentedControl
            fill
            label="Worst finding"
            options={severities.map((value) => ({
              key: value,
              label: label(value),
              selected: severity === value,
              onSelect: () => setSeverity(value),
            }))}
          />
        </div>
      </div>

      <div className={styles.result} aria-live="polite" data-grade={activeKey}>
        <span key={letter} className={styles.letter}>
          {letter}
        </span>
        <div className={styles.resultCopy}>
          <h3>{rejected ? "Not sold" : `Grade ${letter}`}</h3>
          <p>{outcome?.repairRequirement}</p>
        </div>
        <div className={styles.cover}>
          <p className={styles.coverTitle}>
            {rejected ? "Withheld from sale" : "ApexShield cover this grade can take"}
          </p>
          <ul>
            {plans.map((plan) => (
              <li key={plan.id} data-eligible={plan.eligible || undefined}>
                <span className={styles.mark} aria-hidden="true" />
                <span>
                  <strong>{plan.tier}</strong>
                  {plan.detail && <small>{plan.detail}</small>}
                </span>
              </li>
            ))}
          </ul>
          <Link href="/warranty" className={buttonClassName("primary", "md")}>
            Compare warranty plans
          </Link>
        </div>
      </div>
    </div>
  );
}
