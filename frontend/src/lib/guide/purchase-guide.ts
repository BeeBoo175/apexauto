import { isRecord, isUuid } from "@/lib/auth/contracts";
import { formatCurrency } from "@/lib/currency-format";
import { parseItems } from "@/lib/api";

export type InspectionGradeTier = "A" | "B" | "C";

export interface GuideWarranty {
  id: string;
  name: string;
  durationMonths: number;
  mileageCapKm: number;
  price: number;
  minimumGrade: InspectionGradeTier;
}

// The effective purchase terms the buying guide, FAQ and grading simulator explain.
export interface PurchaseGuide {
  processingFee: number;
  sstRate: number;
  warranties: readonly GuideWarranty[];
}

const grades: readonly string[] = ["A", "B", "C"];

function parseWarranty(value: unknown): GuideWarranty | null {
  if (
    !isRecord(value) ||
    !isUuid(value.id) ||
    typeof value.name !== "string" ||
    value.name.length === 0 ||
    !Number.isSafeInteger(value.durationMonths) ||
    !Number.isSafeInteger(value.mileageCapKm) ||
    typeof value.price !== "number" ||
    !Number.isFinite(value.price) ||
    value.price < 0 ||
    typeof value.minimumGrade !== "string" ||
    !grades.includes(value.minimumGrade)
  ) {
    return null;
  }

  return {
    id: value.id,
    name: value.name,
    durationMonths: value.durationMonths as number,
    mileageCapKm: value.mileageCapKm as number,
    price: value.price,
    minimumGrade: value.minimumGrade as InspectionGradeTier,
  };
}

export function parsePurchaseGuide(value: unknown): PurchaseGuide | null {
  if (
    !isRecord(value) ||
    typeof value.processingFee !== "number" ||
    !Number.isFinite(value.processingFee) ||
    value.processingFee < 0 ||
    typeof value.sstRate !== "number" ||
    !Number.isFinite(value.sstRate) ||
    value.sstRate < 0 ||
    value.sstRate > 1
  ) {
    return null;
  }

  const warranties = parseItems(value.warranties, parseWarranty);
  if (!warranties || new Set(warranties.map((item) => item.id)).size !== warranties.length) {
    return null;
  }

  return { processingFee: value.processingFee, sstRate: value.sstRate, warranties };
}

// 0.08 reads as 8%, 0.065 as 6.5%.
export function sstPercentLabel(sstRate: number) {
  return `${Number((sstRate * 100).toFixed(2))}%`;
}

export function guideServiceTax(guide: PurchaseGuide) {
  return Math.round(guide.processingFee * guide.sstRate * 100) / 100;
}

export function guideFeeSummary(guide: PurchaseGuide) {
  return `${formatCurrency(guide.processingFee, "RM")} fee, ${sstPercentLabel(guide.sstRate)} SST (${formatCurrency(guideServiceTax(guide), "RM")})`;
}
