import { isRecord, isUuid } from "@/lib/auth/contracts";
import { parseMyr } from "@/lib/currency-input";
import type { PublicVehicleDetail } from "./public-detail";

export interface PurchasePrice {
  vehiclePrice: number;
  processingFee: number;
  roadTax: number;
  serviceTax: number;
  promoDiscount: number;
  total: number;
}

export interface PurchaseQuote {
  promoCode: string;
  promoId: string;
  price: PurchasePrice;
}

export function promoCode(value: string): string | null {
  const code = value.trim();
  return code && code.length <= 50 && !value.includes("\0") ? code : null;
}

const money = (value: unknown): value is number =>
  typeof value === "number" && Number.isFinite(value) && parseMyr(value.toFixed(2)) === value;

export function parsePurchasePrice(value: unknown): PurchasePrice | null {
  if (
    !isRecord(value) ||
    !money(value.vehiclePrice) ||
    value.vehiclePrice <= 0 ||
    !money(value.processingFee) ||
    !money(value.roadTax) ||
    !money(value.serviceTax) ||
    !money(value.promoDiscount) ||
    value.promoDiscount > value.vehiclePrice ||
    !money(value.total)
  ) {
    return null;
  }
  const sen = (amount: number) => BigInt(Math.round(amount * 100));
  if (
    sen(value.total) !==
    sen(value.vehiclePrice) +
      sen(value.processingFee) +
      sen(value.roadTax) +
      sen(value.serviceTax) -
      sen(value.promoDiscount)
  ) {
    return null;
  }
  return {
    vehiclePrice: value.vehiclePrice,
    processingFee: value.processingFee,
    roadTax: value.roadTax,
    serviceTax: value.serviceTax,
    promoDiscount: value.promoDiscount,
    total: value.total,
  };
}

export function parsePurchaseQuote(value: unknown, expectedCode: string): PurchaseQuote | null {
  if (!isRecord(value) || value.promoCode !== expectedCode || !isUuid(value.promoId)) {
    return null;
  }
  const price = parsePurchasePrice(value.price);
  return price ? { promoCode: expectedCode, promoId: value.promoId, price } : null;
}

export function matchesPurchaseTerms(quote: PurchaseQuote, price: PurchasePrice) {
  return (
    quote.price.vehiclePrice === price.vehiclePrice &&
    quote.price.processingFee === price.processingFee &&
    quote.price.roadTax === price.roadTax &&
    quote.price.serviceTax === price.serviceTax
  );
}

export function purchaseTermsKey(detail: PublicVehicleDetail) {
  return JSON.stringify([
    detail.listing.id.toLowerCase(),
    detail.listing.publishedAt,
    detail.price,
    detail.sstRate,
  ]);
}
