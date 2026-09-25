"use client";

import { useState } from "react";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { SegmentedControl } from "@/components/ui/SegmentedControl";
import { formatWholeCurrency } from "@/lib/currency-format";
import type { GuideWarranty, InspectionGradeTier } from "@/lib/guide/purchase-guide";
import { isWarrantyPlanEligible, splitWarrantyName } from "@/lib/guide/warranty-plans";
import { vehicleGrades } from "@/lib/vehicles/contracts";
import styles from "./WarrantyPlans.module.css";

type GradeFilter = "ALL" | InspectionGradeTier;

const gradeOptions: readonly { value: GradeFilter; label: string }[] = [
  { value: "ALL", label: "All grades" },
  { value: "A", label: "Grade A" },
  { value: "B", label: "Grade B" },
  { value: "C", label: "Grade C" },
];

// Every line comes from the live plan list: the first plan states its terms, the next ones what they add.
function planFeatures(plan: GuideWarranty, previous: GuideWarranty | undefined) {
  const features: string[] = [];
  if (!previous) {
    features.push(`${plan.durationMonths} months of cover`);
    features.push(`Up to ${plan.mileageCapKm.toLocaleString("en-MY")} km`);
  } else {
    const extraMonths = plan.durationMonths - previous.durationMonths;
    const extraKm = plan.mileageCapKm - previous.mileageCapKm;
    features.push(
      extraMonths > 0
        ? `${extraMonths} more months, ${plan.durationMonths} in total`
        : `${plan.durationMonths} months of cover`,
    );
    features.push(
      extraKm > 0
        ? `${extraKm.toLocaleString("en-MY")} km more, up to ${plan.mileageCapKm.toLocaleString("en-MY")} km`
        : `Up to ${plan.mileageCapKm.toLocaleString("en-MY")} km`,
    );
  }
  features.push(`Cars graded ${plan.minimumGrade} or better`);
  return features;
}

export function WarrantyPlans({ warranties }: { warranties: readonly GuideWarranty[] }) {
  const [grade, setGrade] = useState<GradeFilter>("ALL");
  const longestCover = Math.max(...warranties.map((plan) => plan.durationMonths));

  return (
    <div className={styles.wrap}>
      <div className={styles.gradeBar}>
        <p className={styles.gradeLabel}>Show the plans for a car graded</p>
        <SegmentedControl
          label="Car inspection grade"
          options={gradeOptions.map((option) => ({
            key: option.value,
            label: option.label,
            selected: grade === option.value,
            onSelect: () => setGrade(option.value),
          }))}
        />
      </div>

      <ol className={styles.plans}>
        {warranties.map((plan, index) => {
          const { tier, detail } = splitWarrantyName(plan.name);
          const featured = plan.durationMonths === longestCover;
          const eligible = grade === "ALL" || isWarrantyPlanEligible(plan.minimumGrade, grade);
          const previous = index > 0 ? warranties[index - 1] : undefined;
          const previousTier = previous ? splitWarrantyName(previous.name).tier : "";
          // The cars this plan can cover: the chosen grade, or every grade the plan accepts.
          const coveredGrades =
            grade === "ALL"
              ? vehicleGrades.filter((carGrade) =>
                  isWarrantyPlanEligible(plan.minimumGrade, carGrade),
                )
              : [grade];
          const coversEveryGrade = coveredGrades.length === vehicleGrades.length;
          const carsHref = coversEveryGrade
            ? "/vehicles"
            : `/vehicles?grade=${coveredGrades.join(",")}`;
          const carsLabel = coversEveryGrade
            ? "Browse the cars"
            : `Browse Grade ${coveredGrades.join(" or ")} cars`;

          return (
            <li
              key={plan.id}
              className={styles.plan}
              data-featured={featured || undefined}
              data-unavailable={!eligible || undefined}
            >
              {featured && <span className={styles.planTag}>Longest cover</span>}
              <h3 className={styles.tier}>{tier}</h3>
              <p className={styles.detail}>{detail || "ApexShield plan"}</p>

              <p className={styles.price}>{formatWholeCurrency(plan.price)}</p>
              <p className={styles.priceNote}>
                One-time, billed separately from the on-the-road total.
              </p>

              {eligible ? (
                <Link
                  href={carsHref}
                  prefetch={false}
                  className={buttonClassName(
                    featured ? "primary" : "secondary",
                    "md",
                    styles.action,
                  )}
                >
                  {carsLabel}
                </Link>
              ) : (
                <span className={styles.unavailable}>
                  Needs Grade {plan.minimumGrade} or better
                </span>
              )}

              <p className={styles.listLabel}>
                {previous ? `Compared with ${previousTier}:` : "What you get:"}
              </p>
              <ul className={styles.features}>
                {planFeatures(plan, previous).map((feature) => (
                  <li key={feature}>
                    <span className={styles.featureIcon}>
                      <Icon name="check" />
                    </span>
                    <span>{feature}</span>
                  </li>
                ))}
              </ul>
            </li>
          );
        })}
      </ol>

      <h2 className={styles.compareHeading}>Compare the plans</h2>
      <div className={styles.tableWrap}>
        <table className={styles.table}>
          <caption className="sr-only">Comparison of the active ApexShield warranty plans</caption>
          <thead>
            <tr>
              <th scope="col">
                <span className="sr-only">Feature</span>
              </th>
              {warranties.map((plan) => (
                <th key={plan.id} scope="col">
                  <span className={styles.colTier}>{splitWarrantyName(plan.name).tier}</span>
                  <span className={styles.colPrice}>{formatWholeCurrency(plan.price)}</span>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            <tr className={styles.groupRow}>
              <th scope="colgroup" colSpan={warranties.length + 1}>
                Cover
              </th>
            </tr>
            <tr>
              <th scope="row">Coverage duration</th>
              {warranties.map((plan) => (
                <td key={plan.id}>{plan.durationMonths} months</td>
              ))}
            </tr>
            <tr>
              <th scope="row">Mileage limit</th>
              {warranties.map((plan) => (
                <td key={plan.id}>{plan.mileageCapKm.toLocaleString("en-MY")} km</td>
              ))}
            </tr>
            <tr>
              <th scope="row">One-time price</th>
              {warranties.map((plan) => (
                <td key={plan.id}>{formatWholeCurrency(plan.price)}</td>
              ))}
            </tr>
            <tr className={styles.groupRow}>
              <th scope="colgroup" colSpan={warranties.length + 1}>
                Eligible cars
              </th>
            </tr>
            {vehicleGrades.map((carGrade) => (
              <tr key={carGrade}>
                <th scope="row">Grade {carGrade} cars</th>
                {warranties.map((plan) => {
                  const eligible = isWarrantyPlanEligible(plan.minimumGrade, carGrade);
                  return (
                    <td key={plan.id}>
                      {eligible ? (
                        <span className={styles.yes}>
                          <Icon name="check" />
                          <span className="sr-only">Eligible</span>
                        </span>
                      ) : (
                        <span className={styles.no}>
                          <span aria-hidden="true">–</span>
                          <span className="sr-only">Not eligible</span>
                        </span>
                      )}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
