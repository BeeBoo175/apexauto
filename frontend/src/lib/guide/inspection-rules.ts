import type { IconName } from "@/lib/icons";
import { parseMyr } from "@/lib/currency-input";
import { inspectionCategoryIcons, type Severity } from "@/lib/inspection/contracts";
import type { InspectionCategoryKey } from "./inspection-points";

export interface InspectionCategorySummary {
  key: InspectionCategoryKey;
  title: string;
  prefix: string;
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
  eligibleWarrantyLabel: string;
}

export const inspectionCategories: readonly InspectionCategorySummary[] = [
  {
    key: "FrameChassis",
    title: "Frame & Chassis",
    prefix: "FC",
    count: 45,
    icon: inspectionCategoryIcons.FrameChassis,
    focus: "Factory weld seams, chassis rails, pillars, and flood cavity checks.",
  },
  {
    key: "EngineTransmission",
    title: "Engine & Transmission",
    prefix: "ET",
    count: 50,
    icon: inspectionCategoryIcons.EngineTransmission,
    focus: "Cold starts, fluid seals, cooling fans, diagnostics, and gear shifts.",
  },
  {
    key: "ExteriorPaint",
    title: "Exterior & Paint",
    prefix: "EP",
    count: 45,
    icon: inspectionCategoryIcons.ExteriorPaint,
    focus: "Panel gaps, paint-depth consistency, weather seals, glass, and lamps.",
  },
  {
    key: "InteriorElectrical",
    title: "Interior & Electrical",
    prefix: "IE",
    count: 40,
    icon: inspectionCategoryIcons.InteriorElectrical,
    focus: "True odometer checks, airbags, seat belts, cabin AC, and electronics.",
  },
  {
    key: "UnderbodyBrakesSuspension",
    title: "Underbody, Brakes & Suspension",
    prefix: "UB",
    count: 30,
    icon: inspectionCategoryIcons.UnderbodyBrakesSuspension,
    focus: "Tyre tread, brake pads and discs, ABS, dampers, and steering play.",
  },
];

export const inspectionCategoryTitles: Record<InspectionCategoryKey, string> = {
  FrameChassis: "Frame & Chassis",
  EngineTransmission: "Engine & Transmission",
  ExteriorPaint: "Exterior & Paint",
  InteriorElectrical: "Interior & Electrical",
  UnderbodyBrakesSuspension: "Underbody, Brakes & Suspension",
};

export const signOffInvariants: readonly { icon: IconName; title: string; copy: string }[] = [
  {
    icon: "check",
    title: "Every point checked",
    copy: "All 210 checks inspected or marked Not fitted.",
  },
  {
    icon: "tool",
    title: "Fixed before listing",
    copy: "Minor, Major, and paid repairs finish before sale.",
  },
  {
    icon: "shield-check",
    title: "Tamper-proof reports",
    copy: "Signed reports lock and version cleanly on updates.",
  },
];

/**
 * Mirrors InspectionGrading.Calculate in Dealership.Domain:
 * grade is the worse of the estimated-cost threshold and worst-severity floor.
 */
export function evaluateInspectionOutcome(
  rawCost: number,
  worstSeverity: Severity,
): GradingEvaluation | null {
  if (!Number.isFinite(rawCost) || rawCost < 0) {
    return null;
  }
  const estimatedRepairCost = parseMyr(rawCost.toFixed(2));
  if (estimatedRepairCost === null) {
    return null;
  }

  const isCritical = worstSeverity === "CriticalDisqualifier";
  const overBudgetCeiling = estimatedRepairCost > 15000;
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
      repairRequirement: "Not approved for sale (over RM 15,000 or critical defect).",
      eligibleWarrantyLabel: "Not eligible for warranty",
    };
  }

  if (worstSeverity === "MajorDefect" || estimatedRepairCost >= 2500) {
    const sevHit = worstSeverity === "MajorDefect";
    const costHit = estimatedRepairCost >= 2500;
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
      eligibleWarrantyLabel: "Silver (Powertrain) only",
    };
  }

  if (worstSeverity === "MinorDefect" || estimatedRepairCost >= 500) {
    const sevHit = worstSeverity === "MinorDefect";
    const costHit = estimatedRepairCost >= 500;
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
      eligibleWarrantyLabel: "Silver or Gold (Comprehensive)",
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
    eligibleWarrantyLabel: "Silver, Gold, or Platinum (Zero Deductible)",
  };
}
