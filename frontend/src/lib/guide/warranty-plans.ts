export type InspectionGradeTier = "A" | "B" | "C";

export interface WarrantyTierPlan {
  id: string;
  name: string;
  durationMonths: number;
  mileageCapKm: number;
  priceMyr: number;
  minimumGrade: InspectionGradeTier;
  tagline: string;
  highlights: readonly string[];
}

export interface WarrantyMatrixRow {
  feature: string;
  silver: boolean | string;
  gold: boolean | string;
  platinum: boolean | string;
}

const gradeRank: Record<InspectionGradeTier, number> = {
  A: 0,
  B: 1,
  C: 2,
};

export const warrantyPlans: readonly WarrantyTierPlan[] = [
  {
    id: "silver",
    name: "Silver (Powertrain)",
    durationMonths: 12,
    mileageCapKm: 25000,
    priceMyr: 1200,
    minimumGrade: "C",
    tagline: "Essential engine and transmission coverage for every retail grade.",
    highlights: [
      "12 months or 25,000 km coverage",
      "Available on Grade A, B, and C cars",
      "Optional add-on separate from OTR price",
    ],
  },
  {
    id: "gold",
    name: "Gold (Comprehensive)",
    durationMonths: 24,
    mileageCapKm: 50000,
    priceMyr: 2400,
    minimumGrade: "B",
    tagline: "Two-year coverage for Grade A and B cars across cooling, steering, and AC.",
    highlights: [
      "24 months or 50,000 km coverage",
      "Unlocks on Grade A and Grade B cars",
      "Covers engine, gearbox, cooling, steering, and AC",
    ],
  },
  {
    id: "platinum",
    name: "Platinum (Zero Deductible)",
    durationMonths: 36,
    mileageCapKm: 75000,
    priceMyr: 3600,
    minimumGrade: "A",
    tagline: "Three-year zero-deductible coverage exclusively for Grade A vehicles.",
    highlights: [
      "36 months or 75,000 km coverage",
      "Exclusive to Grade A vehicles",
      "Zero deductible on covered mechanical and electrical repairs",
    ],
  },
];

export const warrantyMatrixRows: readonly WarrantyMatrixRow[] = [
  { feature: "Coverage duration", silver: "12 months", gold: "24 months", platinum: "36 months" },
  { feature: "Mileage limit", silver: "25,000 km", gold: "50,000 km", platinum: "75,000 km" },
  {
    feature: "One-time option price",
    silver: "RM 1,200",
    gold: "RM 2,400",
    platinum: "RM 3,600",
  },
  { feature: "Minimum grade", silver: "Grade C", gold: "Grade B", platinum: "Grade A" },
  { feature: "Engine & transmission internals", silver: true, gold: true, platinum: true },
  { feature: "Cooling, steering & climate", silver: false, gold: true, platinum: true },
  { feature: "Zero-deductible claim terms", silver: false, gold: false, platinum: true },
];

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
