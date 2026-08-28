"use client";

import { useState } from "react";
import { formatCurrency } from "@/lib/currency-format";
import type { GuideWarranty, InspectionGradeTier } from "@/lib/guide/purchase-guide";
import { isWarrantyPlanEligible, warrantyMatrixRows } from "@/lib/guide/warranty-plans";
import guideLayout from "./GuideLayout.module.css";
import styles from "./BuyingGuideWarrantySection.module.css";

type GradeFilter = "ALL" | InspectionGradeTier;

const gradeFilterOptions: readonly { value: GradeFilter; label: string }[] = [
  { value: "ALL", label: "All grades" },
  { value: "A", label: "Grade A" },
  { value: "B", label: "Grade B" },
  { value: "C", label: "Grade C" },
];

function planStatusText(plan: GuideWarranty, selectedGrade: GradeFilter, eligible: boolean) {
  const priceLabel = formatCurrency(plan.price, "RM");
  if (selectedGrade === "ALL") {
    return `${priceLabel} · Min Grade ${plan.minimumGrade}`;
  }
  if (eligible) {
    return `${priceLabel} · Eligible`;
  }
  return `${priceLabel} · Requires Grade ${plan.minimumGrade}+`;
}

export function BuyingGuideWarrantySection({
  warranties,
}: {
  warranties: readonly GuideWarranty[];
}) {
  const [selectedGrade, setSelectedGrade] = useState<GradeFilter>("ALL");

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
          <caption className="sr-only">Comparison of the active ApexShield warranty plans</caption>
          <thead>
            <tr>
              <th scope="col">Coverage term</th>
              {warranties.map((plan) => {
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
            {warrantyMatrixRows(warranties).map((row) => (
              <tr key={row.feature}>
                <th scope="row">{row.feature}</th>
                {row.values.map((value, index) => (
                  <td key={warranties[index].id}>{value}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
