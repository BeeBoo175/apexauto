import { formatCurrency } from "@/lib/currency-format";
import type { GuideWarranty, InspectionGradeTier } from "./purchase-guide";

export interface WarrantyMatrixRow {
  feature: string;
  values: readonly string[];
}

const gradeRank: Record<InspectionGradeTier, number> = {
  A: 0,
  B: 1,
  C: 2,
};

/**
 * Mirrors WarrantyPackage.IsEligibleFor: grades rank A (0) < B (1) < C (2);
 * rejected cars (null) have no eligibility.
 */
export function isWarrantyPlanEligible(
  minimumGrade: InspectionGradeTier,
  vehicleGrade: InspectionGradeTier | null,
): boolean {
  if (vehicleGrade === null) {
    return false;
  }
  return gradeRank[vehicleGrade] <= gradeRank[minimumGrade];
}

// The active plans a car of this grade can take, as one readable phrase such as "Silver or Gold".
export function eligibleWarrantyLabel(
  warranties: readonly GuideWarranty[],
  vehicleGrade: InspectionGradeTier | null,
) {
  const names = warranties
    .filter((plan) => isWarrantyPlanEligible(plan.minimumGrade, vehicleGrade))
    .map((plan) => plan.name);
  if (names.length === 0) {
    return "Not eligible for warranty";
  }
  if (names.length === 1) {
    return names[0];
  }
  return `${names.slice(0, -1).join(", ")} or ${names[names.length - 1]}`;
}

// One row per term, one column per active plan, in the order the API lists them (cheapest first).
export function warrantyMatrixRows(
  warranties: readonly GuideWarranty[],
): readonly WarrantyMatrixRow[] {
  return [
    {
      feature: "Coverage duration",
      values: warranties.map((plan) => `${plan.durationMonths} months`),
    },
    {
      feature: "Mileage limit",
      values: warranties.map((plan) => `${plan.mileageCapKm.toLocaleString("en-MY")} km`),
    },
    {
      feature: "One-time option price",
      values: warranties.map((plan) => formatCurrency(plan.price, "RM")),
    },
    {
      feature: "Minimum grade",
      values: warranties.map((plan) => `Grade ${plan.minimumGrade}`),
    },
  ];
}
