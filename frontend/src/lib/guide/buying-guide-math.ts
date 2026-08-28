import { parseMyr } from "@/lib/currency-input";
import { exactMyr, minorUnits, roundedDivision } from "@/lib/vehicles/financing";
import type { PurchasePrice } from "@/lib/vehicles/purchase-quote";

export interface GuidePriceTier {
  id: string;
  label: string;
  vehiclePrice: number;
  roadTax: number;
  summary: string;
}

export interface GuideSamplePromo {
  code: string;
  label: string;
  discount: number;
}

export const guidePriceTiers: readonly GuidePriceTier[] = [
  {
    id: "compact",
    label: "Compact hatchback",
    vehiclePrice: 50000,
    roadTax: 90,
    summary: "Illustrative RM 90 road tax example for small-displacement private cars",
  },
  {
    id: "sedan-suv",
    label: "Family sedan / SUV",
    vehiclePrice: 80000,
    roadTax: 280,
    summary: "Illustrative RM 280 road tax example for mid-size 1.5L-1.8L vehicles",
  },
  {
    id: "executive",
    label: "Executive / hybrid",
    vehiclePrice: 120000,
    roadTax: 480,
    summary: "Illustrative RM 480 road tax example for higher-displacement vehicles",
  },
];

export const guideRoadTaxOptions: readonly { label: string; amount: number }[] = [
  { label: "RM 90 (Compact)", amount: 90 },
  { label: "RM 280 (Mid-size)", amount: 280 },
  { label: "RM 480 (Larger)", amount: 480 },
];

export const guideSamplePromos: readonly GuideSamplePromo[] = [
  { code: "NONE", label: "No voucher", discount: 0 },
  { code: "APEX500", label: "APEX500 (−RM 500)", discount: 500 },
  { code: "RAYA1000", label: "RAYA1000 (−RM 1,000)", discount: 1000 },
];

export function percentageOfMyr(amount: number, percent: number): string | null {
  const sen = minorUnits(amount);
  if (sen === null || !Number.isInteger(percent) || percent < 0 || percent > 100) {
    return null;
  }
  const value = exactMyr(roundedDivision(sen * BigInt(percent), BigInt(100)));
  return value === null ? null : value.toFixed(2);
}

/**
 * Mirrors PurchasePricing.CalculatePrice: SST applies to processingFee,
 * and total = vehiclePrice - promoDiscount + processingFee + roadTax + serviceTax.
 */
export function calculateGuideOtr(
  rawVehiclePrice: number,
  rawRoadTax: number,
  rawPromoDiscount: number,
  rawProcessingFee: number,
  sstRate: number,
): PurchasePrice | null {
  if (
    !Number.isFinite(rawVehiclePrice) ||
    rawVehiclePrice <= 0 ||
    !Number.isFinite(rawRoadTax) ||
    rawRoadTax < 0 ||
    !Number.isFinite(rawPromoDiscount) ||
    rawPromoDiscount < 0 ||
    !Number.isFinite(rawProcessingFee) ||
    rawProcessingFee < 0 ||
    !Number.isFinite(sstRate) ||
    sstRate < 0 ||
    sstRate > 1
  ) {
    return null;
  }

  const vehiclePrice = parseMyr(rawVehiclePrice.toFixed(2));
  const roadTax = parseMyr(rawRoadTax.toFixed(2));
  const processingFee = parseMyr(rawProcessingFee.toFixed(2));
  const promoRequested = parseMyr(rawPromoDiscount.toFixed(2));
  if (
    vehiclePrice === null ||
    vehiclePrice <= 0 ||
    roadTax === null ||
    processingFee === null ||
    promoRequested === null
  ) {
    return null;
  }

  const priceSen = minorUnits(vehiclePrice);
  const feeSen = minorUnits(processingFee);
  const roadTaxSen = minorUnits(roadTax);
  const promoRequestedSen = minorUnits(promoRequested);
  if (priceSen === null || feeSen === null || roadTaxSen === null || promoRequestedSen === null) {
    return null;
  }

  const promoSen = promoRequested > vehiclePrice ? priceSen : promoRequestedSen;
  const sstBasisPoints = BigInt(Math.round(sstRate * 10000));
  const serviceTaxSen = roundedDivision(feeSen * sstBasisPoints, BigInt(10000));
  const totalSen = priceSen - promoSen + feeSen + roadTaxSen + serviceTaxSen;

  const promoDiscount = exactMyr(promoSen);
  const serviceTax = exactMyr(serviceTaxSen);
  const total = exactMyr(totalSen);
  if (promoDiscount === null || serviceTax === null || total === null) {
    return null;
  }

  return {
    vehiclePrice,
    processingFee,
    roadTax,
    serviceTax,
    promoDiscount,
    total,
  };
}
