import type { PublicVehicleDetail } from "./public-detail";

type ChecklistPoint = PublicVehicleDetail["checklist"]["points"][number];

export interface InspectionAreaTotals {
  category: string;
  code: string;
  total: number;
  passed: number;
  notFitted: number;
  findingCount: number;
}

// One row per inspection area, in the order each area first appears in the signed report.
export function summariseInspectionAreas(
  points: readonly ChecklistPoint[],
): InspectionAreaTotals[] {
  const areas = new Map<string, InspectionAreaTotals>();

  for (const point of points) {
    const area = areas.get(point.category) ?? {
      category: point.category,
      code: point.code.split("-")[0],
      total: 0,
      passed: 0,
      notFitted: 0,
      findingCount: 0,
    };
    area.total += 1;
    if (point.kind === null) {
      area.passed += 1;
    }
    if (point.kind === "NotFitted") {
      area.notFitted += 1;
    }
    if (point.kind === "Finding") {
      area.findingCount += 1;
    }
    areas.set(point.category, area);
  }

  return [...areas.values()];
}
