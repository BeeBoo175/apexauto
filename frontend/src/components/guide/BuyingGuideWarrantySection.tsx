"use client";

import { useState } from "react";
import { Icon } from "@/components/ui/Icon";
import { formatCurrency } from "@/lib/currency-format";
import {
  isWarrantyPlanEligible,
  warrantyMatrixRows,
  warrantyPlans,
  type InspectionGradeTier,
  type WarrantyTierPlan,
} from "@/lib/guide/warranty-plans";
import guideLayout from "./GuideLayout.module.css";
import styles from "./BuyingGuideWarrantySection.module.css";

type GradeFilter = "ALL" | InspectionGradeTier;

const gradeFilterOptions: readonly { value: GradeFilter; label: string }[] = [
  { value: "ALL", label: "All grades" },
  { value: "A", label: "Grade A" },
  { value: "B", label: "Grade B" },
  { value: "C", label: "Grade C" },
];

function renderMatrixCell(value: boolean | string) {
  if (value === true) {
    return (
      <span className={styles.includedCell}>
        <Icon name="check" />
        <span>Included</span>
      </span>
    );
  }
  if (value === false) {
    return (
      <span className={styles.excludedCell}>
        <Icon name="close" />
        <span>Not included</span>
      </span>
    );
  }
  return value;
}

function planStatusText(plan: WarrantyTierPlan, selectedGrade: GradeFilter, eligible: boolean) {
  const priceLabel = formatCurrency(plan.priceMyr);
  if (selectedGrade === "ALL") {
    return `${priceLabel} · Min Grade ${plan.minimumGrade}`;
  }
  if (eligible) {
    return `${priceLabel} · Eligible`;
  }
  return `${priceLabel} · Requires Grade ${plan.minimumGrade}+`;
}

export function BuyingGuideWarrantySection() {
  const [selectedGrade, setSelectedGrade] = useState<GradeFilter>("ALL");

  const planByIndex = [warrantyPlans[0], warrantyPlans[1], warrantyPlans[2]] as const;

  return (
    <div className={styles.warrantyWrap}>
      <div className={styles.warrantyToolbar}>
        <span className={styles.filterCaption}>Filter eligible coverage by car grade</span>

        <div className={guideLayout.chipRow} role="group" aria-label="Filter by vehicle grade">
          {gradeFilterOptions.map((option) => (
            <button
              key={option.value}
              type="button"
              className={guideLayout.chip}
              aria-pressed={selectedGrade === option.value}
              onClick={() => setSelectedGrade(option.value)}
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>

      <div className={styles.matrixWrap}>
        <table className={styles.matrix}>
          <caption className="sr-only">
            Comparison of ApexShield Silver, Gold and Platinum warranty plans
          </caption>
          <thead>
            <tr>
              <th scope="col">Coverage term</th>
              {planByIndex.map((plan) => {
                const eligible =
                  selectedGrade === "ALL"
                    ? true
                    : isWarrantyPlanEligible(plan.minimumGrade, selectedGrade);
                const statusLabel = planStatusText(plan, selectedGrade, eligible);
                let headerClass: string | undefined;
                if (selectedGrade !== "ALL") {
                  headerClass = eligible ? styles.colEligible : styles.colIneligible;
                }
                return (
                  <th key={plan.id} scope="col" className={headerClass}>
                    {plan.name}
                    <small>{statusLabel}</small>
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody>
            {warrantyMatrixRows.map((row) => (
              <tr key={row.feature}>
                <th scope="row">{row.feature}</th>
                <td>{renderMatrixCell(row.silver)}</td>
                <td>{renderMatrixCell(row.gold)}</td>
                <td>{renderMatrixCell(row.platinum)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
