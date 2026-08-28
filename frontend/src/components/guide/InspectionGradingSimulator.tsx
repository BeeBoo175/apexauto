"use client";

import { useState } from "react";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { formatCurrency } from "@/lib/currency-format";
import type { InspectionGradingPolicy } from "@/lib/guide/inspection-points";
import type { GuideWarranty } from "@/lib/guide/purchase-guide";
import { eligibleWarrantyLabel } from "@/lib/guide/warranty-plans";
import { parseMyr } from "@/lib/currency-input";
import { evaluateInspectionOutcome, type GradingEvaluation } from "@/lib/guide/inspection-rules";
import { label, severities, type Severity } from "@/lib/inspection/contracts";
import guideLayout from "./GuideLayout.module.css";
import styles from "./InspectionGradingSimulator.module.css";

const costPresets = [
  { label: "RM 350", value: "350.00" },
  { label: "RM 1,200", value: "1200.00" },
  { label: "RM 3,500", value: "3500.00" },
  { label: "RM 16,000", value: "16000.00" },
] as const;

const driverLabels: Record<GradingEvaluation["driver"], string> = {
  baseline: "Advisory only",
  cost: "Repair cost threshold",
  severity: "Worst finding severity cap",
  both: "Both cost and severity limit",
};

export function InspectionGradingSimulator({
  policy,
  pointCount,
  warranties,
}: {
  policy: InspectionGradingPolicy;
  pointCount: number;
  warranties: readonly GuideWarranty[];
}) {
  const gradeBMinimum = formatCurrency(policy.gradeBMinimumRepairCost);
  const gradeCMinimum = formatCurrency(policy.gradeCMinimumRepairCost);
  const maximumRepair = formatCurrency(policy.maximumRepairCost);
  const gradeBMaximum = formatCurrency(policy.gradeCMinimumRepairCost - 0.01);
  const gradeScaleSteps = [
    { id: "A", title: "Grade A", range: `< ${gradeBMinimum}` },
    { id: "B", title: "Grade B", range: `${gradeBMinimum} – ${gradeBMaximum}` },
    { id: "C", title: "Grade C", range: `${gradeCMinimum} – ${maximumRepair}` },
    { id: "REJECT", title: "Rejected", range: `> ${maximumRepair} / Critical` },
  ];
  const [worstSeverity, setWorstSeverity] = useState<Severity>("Advisory");
  const [costDraft, setCostDraft] = useState("350.00");

  const parsedCost = parseMyr(costDraft);
  const evaluation =
    parsedCost !== null ? evaluateInspectionOutcome(parsedCost, worstSeverity, policy) : null;
  const activeTierId = evaluation?.isRejected ? "REJECT" : (evaluation?.grade ?? "");

  return (
    <div className={styles.simulatorStack}>
      <div className={styles.tierMatrixWrap}>
        <div className={styles.tierMatrixHeader}>
          <h3 className={layout.cardHeading}>Grade comparison tiers</h3>
          <span className={styles.tierMatrixHint}>Select a grade card to load its rules below</span>
        </div>
        <div className={styles.tierGrid} role="group" aria-label="Inspection grade tier presets">
          <button
            type="button"
            className={styles.tierCard}
            aria-pressed={activeTierId === "A"}
            onClick={() => {
              setCostDraft("0");
              setWorstSeverity("Advisory");
            }}
          >
            <div className={styles.tierCardTop}>
              <span className={styles.tierBadge}>Grade A</span>
              <span className={styles.tierStatus}>Approved</span>
            </div>
            <span className={styles.tierThreshold}>
              &lt; {gradeBMinimum} repairs · Advisory max
            </span>
            <span className={styles.tierDetail}>
              Top showroom condition; {eligibleWarrantyLabel(warranties, "A")} eligible
            </span>
          </button>
          <button
            type="button"
            className={styles.tierCard}
            aria-pressed={activeTierId === "B"}
            onClick={() => {
              setCostDraft(policy.gradeBMinimumRepairCost.toFixed(2));
              setWorstSeverity("MinorDefect");
            }}
          >
            <div className={styles.tierCardTop}>
              <span className={styles.tierBadge}>Grade B</span>
              <span className={styles.tierStatus}>Approved</span>
            </div>
            <span className={styles.tierThreshold}>
              {gradeBMinimum}–{gradeBMaximum} · Minor wear max
            </span>
            <span className={styles.tierDetail}>
              All minor items repaired; {eligibleWarrantyLabel(warranties, "B")} eligible
            </span>
          </button>
          <button
            type="button"
            className={styles.tierCard}
            aria-pressed={activeTierId === "C"}
            onClick={() => {
              setCostDraft(policy.gradeCMinimumRepairCost.toFixed(2));
              setWorstSeverity("MajorDefect");
            }}
          >
            <div className={styles.tierCardTop}>
              <span className={styles.tierBadge}>Grade C</span>
              <span className={styles.tierStatus}>Approved</span>
            </div>
            <span className={styles.tierThreshold}>
              {gradeCMinimum}–{maximumRepair} · Major repair
            </span>
            <span className={styles.tierDetail}>
              All major repairs done first; {eligibleWarrantyLabel(warranties, "C")} only
            </span>
          </button>
          <button
            type="button"
            className={styles.tierCard}
            aria-pressed={activeTierId === "REJECT"}
            onClick={() => {
              setCostDraft("0");
              setWorstSeverity("CriticalDisqualifier");
            }}
          >
            <div className={styles.tierCardTop}>
              <span className={styles.tierBadge}>Rejected</span>
              <span className={`${styles.tierStatus} ${styles.tierStatusRejected}`}>Withheld</span>
            </div>
            <span className={styles.tierThreshold}>&gt; {maximumRepair} or Critical defect</span>
            <span className={styles.tierDetail}>
              Flood, structural weld, or tampering — never listed
            </span>
          </button>
        </div>
      </div>

      <div className={styles.simulatorLayout}>
        <div className={`${styles.simulatorControls} ${layout.calmFields}`}>
          <div className={styles.controlGroup}>
            <span className={styles.controlLabel}>
              Worst issue found across {pointCount} checks
            </span>
            <div className={guideLayout.chipRow} role="group" aria-label="Worst issue severity">
              {severities.map((severity) => (
                <button
                  key={severity}
                  type="button"
                  className={guideLayout.chip}
                  aria-pressed={worstSeverity === severity}
                  onClick={() => setWorstSeverity(severity)}
                >
                  {label(severity)}
                </button>
              ))}
            </div>
          </div>

          <div className={styles.controlGroup}>
            <span className={styles.controlLabel}>Estimated repair cost</span>
            <div className={guideLayout.chipRow} role="group" aria-label="Repair estimate presets">
              {costPresets.map((preset) => (
                <button
                  key={preset.value}
                  type="button"
                  className={guideLayout.chip}
                  aria-pressed={parsedCost === Number(preset.value)}
                  onClick={() => setCostDraft(preset.value)}
                >
                  {preset.label}
                </button>
              ))}
            </div>
            <Input
              id="guide-sim-cost"
              label="Repair estimate (RM)"
              inputMode="decimal"
              maxLength={12}
              value={costDraft}
              aria-invalid={evaluation === null ? true : undefined}
              onChange={(event) => setCostDraft(event.target.value)}
            />
            <p className={styles.thresholdHint}>
              Minor/Major defects and paid repair items complete workshop work before listing.
            </p>
          </div>
        </div>

        <aside className={guideLayout.receiptPanel} aria-label="Inspection grade outcome">
          {evaluation === null ? (
            <p className={styles.fieldError}>
              Enter a valid RM repair amount (for example 350.00) to calculate the grade.
            </p>
          ) : (
            <>
              <div className={guideLayout.receiptHeader} aria-live="polite" aria-atomic="true">
                <h3 className={layout.cardHeading}>Resulting grade</h3>
                <strong className={styles.outcomeValue}>
                  {evaluation.isRejected ? "Rejected (off sale)" : `Grade ${evaluation.grade}`}
                </strong>
              </div>

              <div className={styles.bandScale} aria-hidden="true">
                {gradeScaleSteps.map((step) => (
                  <span
                    key={step.id}
                    className={
                      activeTierId === step.id
                        ? `${styles.scaleStep} ${styles.scaleStepActive}`
                        : styles.scaleStep
                    }
                  >
                    <span>{step.title}</span>
                    <span>{step.range}</span>
                  </span>
                ))}
              </div>

              <dl className={guideLayout.ledger}>
                <div className={guideLayout.ledgerRow}>
                  <dt>
                    Repair estimate
                    <span className={guideLayout.ledgerSubline}>
                      {label(evaluation.worstSeverity)} · {driverLabels[evaluation.driver]}
                    </span>
                  </dt>
                  <dd>{formatCurrency(evaluation.estimatedRepairCost)}</dd>
                </div>
                <div className={guideLayout.ledgerRow}>
                  <dt>Eligible warranty</dt>
                  <dd>{eligibleWarrantyLabel(warranties, evaluation.grade)}</dd>
                </div>
                <div className={guideLayout.ledgerRow}>
                  <dt>Before sale</dt>
                  <dd>{evaluation.repairRequirement}</dd>
                </div>
              </dl>

              <div>
                <Link href="/buying-guide#warranty" className={buttonClassName("secondary", "md")}>
                  Compare warranty plans
                </Link>
              </div>
            </>
          )}
        </aside>
      </div>
    </div>
  );
}
