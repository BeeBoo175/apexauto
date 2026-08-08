"use client";

import { useState } from "react";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { formatCurrency } from "@/lib/currency-format";
import { parseMyr } from "@/lib/currency-input";
import { evaluateInspectionOutcome, type GradingEvaluation } from "@/lib/guide/inspection-rules";
import { label, severities, type Severity } from "@/lib/inspection/contracts";
import guideLayout from "./GuideLayout.module.css";
import styles from "./GuideInspection.module.css";

const costPresets = [
  { label: "RM 350", value: "350.00" },
  { label: "RM 1,200", value: "1200.00" },
  { label: "RM 3,500", value: "3500.00" },
  { label: "RM 16,000", value: "16000.00" },
] as const;

const driverLabels: Record<GradingEvaluation["driver"], string> = {
  baseline: "Advisory only, under RM 500",
  cost: "Repair cost threshold",
  severity: "Worst finding severity cap",
  both: "Both cost and severity limit",
};

const gradeScaleSteps = [
  { id: "A", title: "Grade A", range: "< RM 500" },
  { id: "B", title: "Grade B", range: "RM 500 - 2,499.99" },
  { id: "C", title: "Grade C", range: "RM 2,500 - 15,000" },
  { id: "REJECT", title: "Rejected", range: "> RM 15k / Critical" },
] as const;

export function InspectionGradingSimulator() {
  const [worstSeverity, setWorstSeverity] = useState<Severity>("Advisory");
  const [costDraft, setCostDraft] = useState("350.00");

  const parsedCost = parseMyr(costDraft);
  const evaluation =
    parsedCost !== null ? evaluateInspectionOutcome(parsedCost, worstSeverity) : null;
  const activeTierId = evaluation?.isRejected ? "REJECT" : (evaluation?.grade ?? "");

  return (
    <div className={styles.simulatorLayout}>
      <div className={`${styles.simulatorControls} ${layout.calmFields}`}>
        <div className={styles.controlGroup}>
          <span className={styles.controlLabel}>Worst issue found across 210 checks</span>
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
            label="Repair estimate (MYR)"
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
            Enter a valid MYR repair amount (for example 350.00) to calculate the grade.
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
                <dd>{evaluation.eligibleWarrantyLabel}</dd>
              </div>
              <div className={guideLayout.ledgerRow}>
                <dt>Before sale</dt>
                <dd>{evaluation.repairRequirement}</dd>
              </div>
            </dl>

            <div>
              <Link
                href="/buying-guide#warranty"
                prefetch={false}
                className={buttonClassName("secondary", "md")}
              >
                Compare warranty plans
              </Link>
            </div>
          </>
        )}
      </aside>
    </div>
  );
}
