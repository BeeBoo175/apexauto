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
            <span className={styles.tierThreshold}>&lt; RM 500 repairs · Advisory max</span>
            <span className={styles.tierDetail}>
              Top showroom condition; Platinum warranty eligible
            </span>
          </button>
          <button
            type="button"
            className={styles.tierCard}
            aria-pressed={activeTierId === "B"}
            onClick={() => {
              setCostDraft("950");
              setWorstSeverity("MinorDefect");
            }}
          >
            <div className={styles.tierCardTop}>
              <span className={styles.tierBadge}>Grade B</span>
              <span className={styles.tierStatus}>Approved</span>
            </div>
            <span className={styles.tierThreshold}>RM 500–2,499 · Minor wear max</span>
            <span className={styles.tierDetail}>
              All minor items repaired; Gold or Silver eligible
            </span>
          </button>
          <button
            type="button"
            className={styles.tierCard}
            aria-pressed={activeTierId === "C"}
            onClick={() => {
              setCostDraft("3800");
              setWorstSeverity("MajorDefect");
            }}
          >
            <div className={styles.tierCardTop}>
              <span className={styles.tierBadge}>Grade C</span>
              <span className={styles.tierStatus}>Approved</span>
            </div>
            <span className={styles.tierThreshold}>RM 2,500–15,000 · Major repair</span>
            <span className={styles.tierDetail}>
              All major repairs done first; Silver Powertrain only
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
            <span className={styles.tierThreshold}>&gt; RM 15,000 or Critical defect</span>
            <span className={styles.tierDetail}>
              Flood, structural weld, or tampering — never listed
            </span>
          </button>
        </div>
      </div>

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
                  <dd>{evaluation.eligibleWarrantyLabel}</dd>
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
