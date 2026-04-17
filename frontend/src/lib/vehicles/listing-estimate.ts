import { isRecord, isUuid } from "@/lib/auth/contracts";
import { isMyr } from "@/lib/currency-input";
import { parsePurchasePrice, type PurchasePrice } from "./purchase-quote";

export interface ListingEstimateAmounts {
  vehiclePrice: number;
  roadTax: number;
}

export interface ListingEstimate {
  actorId: string;
  vehicleId: string;
  branchId: string;
  price: PurchasePrice;
  sstRate: number;
}

export function parseListingEstimateAmounts(value: unknown): ListingEstimateAmounts | null {
  if (
    !isRecord(value) ||
    !isMyr(value.vehiclePrice) ||
    value.vehiclePrice <= 0 ||
    !isMyr(value.roadTax)
  ) {
    return null;
  }
  return { vehiclePrice: value.vehiclePrice, roadTax: value.roadTax };
}

export function parseListingEstimate(value: unknown): ListingEstimate | null {
  if (
    !isRecord(value) ||
    !isUuid(value.actorId) ||
    !isUuid(value.vehicleId) ||
    !isUuid(value.branchId) ||
    typeof value.sstRate !== "number" ||
    !Number.isFinite(value.sstRate) ||
    value.sstRate < 0 ||
    value.sstRate > 1
  ) {
    return null;
  }
  const price = parsePurchasePrice(value.price);
  if (!price || price.promoDiscount !== 0) {
    return null;
  }
  return {
    actorId: value.actorId,
    vehicleId: value.vehicleId,
    branchId: value.branchId,
    price,
    sstRate: value.sstRate,
  };
}

export function matchesListingEstimate(
  estimate: ListingEstimate,
  expected: ListingEstimateAmounts & { actorId: string; vehicleId: string; branchId: string },
) {
  return (
    estimate.actorId === expected.actorId &&
    estimate.vehicleId === expected.vehicleId &&
    estimate.branchId === expected.branchId &&
    estimate.price.vehiclePrice === expected.vehiclePrice &&
    estimate.price.roadTax === expected.roadTax
  );
}
