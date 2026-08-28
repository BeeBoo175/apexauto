import { parseItems } from "@/lib/api";
import { isRecord } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { checklistCategories } from "@/lib/inspection/contracts";
import { enumValue, parsePoint } from "@/lib/inspection/parse";

export type InspectionCategoryKey = (typeof checklistCategories)[number];

export interface InspectionPointItem {
  code: string;
  category: InspectionCategoryKey;
  description: string;
  disqualifierRisk: boolean;
}

export interface InspectionGradingPolicy {
  gradeBMinimumRepairCost: number;
  gradeCMinimumRepairCost: number;
  maximumRepairCost: number;
}

export interface InspectionGuide {
  catalogVersion: string;
  points: InspectionPointItem[];
  grading: InspectionGradingPolicy;
}

// These highlights help readers find examples; the signed finding determines severity.
const disqualifierHighlightCodes = new Set(["FC-038", "FC-042", "FC-043", "IE-003"]);

function parseGuidePoint(value: unknown): InspectionPointItem | null {
  const point = parsePoint(value);
  const category = enumValue(checklistCategories, point?.category);
  if (!point || !category) {
    return null;
  }
  return { ...point, category, disqualifierRisk: disqualifierHighlightCodes.has(point.code) };
}

export function parseInspectionGuide(value: unknown): InspectionGuide | null {
  if (
    !isRecord(value) ||
    typeof value.catalogVersion !== "string" ||
    !value.catalogVersion.trim() ||
    !isRecord(value.grading)
  ) {
    return null;
  }
  const points = parseItems(value.points, parseGuidePoint);
  const { gradeBMinimumRepairCost, gradeCMinimumRepairCost, maximumRepairCost } = value.grading;
  if (
    !points?.length ||
    new Set(points.map((point) => point.code)).size !== points.length ||
    !checklistCategories.every((category) => points.some((point) => point.category === category)) ||
    !isMyr(gradeBMinimumRepairCost) ||
    !isMyr(gradeCMinimumRepairCost) ||
    !isMyr(maximumRepairCost) ||
    gradeBMinimumRepairCost <= 0 ||
    gradeCMinimumRepairCost <= gradeBMinimumRepairCost ||
    maximumRepairCost < gradeCMinimumRepairCost
  ) {
    return null;
  }
  return {
    catalogVersion: value.catalogVersion,
    points,
    grading: { gradeBMinimumRepairCost, gradeCMinimumRepairCost, maximumRepairCost },
  };
}
