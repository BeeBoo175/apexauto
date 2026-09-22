import type { IconName } from "@/lib/icons";
import { parseMyr } from "@/lib/currency-input";
import { inspectionCategoryIcons, type Severity } from "@/lib/inspection/contracts";
import { formatCurrency } from "@/lib/currency-format";
import type {
  InspectionGradingPolicy,
  InspectionPointItem,
  InspectionCategoryKey,
} from "./inspection-points";

export interface InspectionCategorySummary {
  key: InspectionCategoryKey;
  title: string;
  count: number;
  icon: IconName;
  focus: string;
}

export interface GradingEvaluation {
  grade: "A" | "B" | "C" | null;
  isRejected: boolean;
  estimatedRepairCost: number;
  worstSeverity: Severity;
  driver: "severity" | "cost" | "both" | "baseline";
  repairRequirement: string;
}

const categoryPresentation: readonly Omit<InspectionCategorySummary, "count">[] = [
  {
    key: "FrameChassis",
    title: "Frame & Chassis",
    icon: inspectionCategoryIcons.FrameChassis,
    focus: "Factory weld seams, chassis rails, pillars, and flood cavity checks.",
  },
  {
    key: "EngineTransmission",
    title: "Engine & Transmission",
    icon: inspectionCategoryIcons.EngineTransmission,
    focus: "Cold starts, fluid seals, cooling fans, diagnostics, and gear shifts.",
  },
  {
    key: "ExteriorPaint",
    title: "Exterior & Paint",
    icon: inspectionCategoryIcons.ExteriorPaint,
    focus: "Panel gaps, paint-thickness consistency, weather seals, glass, and lamps.",
  },
  {
    key: "InteriorElectrical",
    title: "Interior & Electrical",
    icon: inspectionCategoryIcons.InteriorElectrical,
    focus: "True odometer checks, airbags, seat belts, cabin AC, and electronics.",
  },
  {
    key: "UnderbodyBrakesSuspension",
    title: "Underbody, Brakes & Suspension",
    icon: inspectionCategoryIcons.UnderbodyBrakesSuspension,
    focus: "Tyre tread, brake pads and discs, ABS, dampers, and steering play.",
  },
];

export function inspectionCategories(
  points: readonly InspectionPointItem[],
): InspectionCategorySummary[] {
  return categoryPresentation.map((category) => ({
    ...category,
    count: points.filter((point) => point.category === category.key).length,
  }));
}

export const inspectionCategoryTitles: Record<InspectionCategoryKey, string> = {
  FrameChassis: "Frame & Chassis",
  EngineTransmission: "Engine & Transmission",
  ExteriorPaint: "Exterior & Paint",
  InteriorElectrical: "Interior & Electrical",
  UnderbodyBrakesSuspension: "Underbody, Brakes & Suspension",
};

/**
 * Mirrors InspectionGrading.Calculate in Dealership.Domain:
 * grade is the worse of the estimated-cost threshold and worst-severity floor.
 */
export function evaluateInspectionOutcome(
  rawCost: number,
  worstSeverity: Severity,
  policy: InspectionGradingPolicy,
): GradingEvaluation | null {
  if (!Number.isFinite(rawCost) || rawCost < 0) {
    return null;
  }
  const estimatedRepairCost = parseMyr(rawCost.toFixed(2));
  if (estimatedRepairCost === null) {
    return null;
  }

  const isCritical = worstSeverity === "CriticalDisqualifier";
  const overBudgetCeiling = estimatedRepairCost > policy.maximumRepairCost;
  if (isCritical || overBudgetCeiling) {
    let driver: GradingEvaluation["driver"] = "cost";
    if (isCritical && overBudgetCeiling) {
      driver = "both";
    } else if (isCritical) {
      driver = "severity";
    }
    return {
      grade: null,
      isRejected: true,
      estimatedRepairCost,
      worstSeverity,
      driver,
      repairRequirement: `Not approved for sale (over ${formatCurrency(policy.maximumRepairCost)} or critical defect).`,
    };
  }

  if (worstSeverity === "MajorDefect" || estimatedRepairCost >= policy.gradeCMinimumRepairCost) {
    const sevHit = worstSeverity === "MajorDefect";
    const costHit = estimatedRepairCost >= policy.gradeCMinimumRepairCost;
    let driver: GradingEvaluation["driver"] = "cost";
    if (sevHit && costHit) {
      driver = "both";
    } else if (sevHit) {
      driver = "severity";
    }
    return {
      grade: "C",
      isRejected: false,
      estimatedRepairCost,
      worstSeverity,
      driver,
      repairRequirement: "All Major and Minor repairs completed before sale.",
    };
  }

  if (worstSeverity === "MinorDefect" || estimatedRepairCost >= policy.gradeBMinimumRepairCost) {
    const sevHit = worstSeverity === "MinorDefect";
    const costHit = estimatedRepairCost >= policy.gradeBMinimumRepairCost;
    let driver: GradingEvaluation["driver"] = "cost";
    if (sevHit && costHit) {
      driver = "both";
    } else if (sevHit) {
      driver = "severity";
    }
    return {
      grade: "B",
      isRejected: false,
      estimatedRepairCost,
      worstSeverity,
      driver,
      repairRequirement: "All Minor repairs completed before sale.",
    };
  }

  return {
    grade: "A",
    isRejected: false,
    estimatedRepairCost,
    worstSeverity,
    driver: "baseline",
    repairRequirement:
      estimatedRepairCost > 0
        ? "Estimated repairs completed before sale; RM 0 advisories noted only."
        : "Ready for sale; zero-cost advisories noted transparently.",
  };
}
