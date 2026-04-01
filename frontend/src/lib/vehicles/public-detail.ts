import { parseItems } from "@/lib/api";
import { isRecord, isUuid } from "@/lib/auth/contracts";
import { severities, type Point, type Severity } from "@/lib/inspection/contracts";
import { enumValue, parsePoint, timestamp } from "@/lib/inspection/parse";
import { parsePublicVehicle } from "./parse";
import type { PublicVehicle } from "./contracts";

type ChecklistPoint = Point & {
  kind: "Finding" | "NotFitted" | null;
  severity: Severity | null;
  estimatedRepairCost: number;
};

export interface PublicVehicleDetail {
  listing: PublicVehicle;
  roadTax: number;
  description: string;
  photoIds: string[];
  sstRate: number;
  price: {
    vehiclePrice: number;
    processingFee: number;
    roadTax: number;
    serviceTax: number;
    promoDiscount: number;
    total: number;
  };
  checklist: {
    catalogVersion: string;
    grade: "A" | "B" | "C" | null;
    signedAt: string;
    points: ChecklistPoint[];
  };
}

const money = (value: unknown): value is number =>
  typeof value === "number" &&
  Number.isFinite(value) &&
  value >= 0 &&
  Number.isSafeInteger(Math.round(value * 100)) &&
  Math.abs(value * 100 - Math.round(value * 100)) < 0.000001;

function parseChecklistPoint(value: unknown): ChecklistPoint | null {
  const point = parsePoint(value);
  if (!point || !isRecord(value) || !money(value.estimatedRepairCost)) {
    return null;
  }
  const kind = enumValue(["Finding", "NotFitted"] as const, value.kind);
  const severity = enumValue(severities, value.severity);
  if (
    (value.kind !== null && !kind) ||
    (kind === "Finding" && !severity) ||
    (kind !== "Finding" && (value.severity !== null || value.estimatedRepairCost !== 0))
  ) {
    return null;
  }
  return { ...point, kind, severity, estimatedRepairCost: value.estimatedRepairCost };
}

export function parsePublicDetail(value: unknown, expectedId?: string): PublicVehicleDetail | null {
  if (!isRecord(value) || !isRecord(value.price) || !isRecord(value.checklist)) {
    return null;
  }
  const listing = parsePublicVehicle(value.listing);
  const price = value.price;
  const checklist = value.checklist;
  const photoIds = parseItems(value.photoIds, (id) => (isUuid(id) ? id : null));
  const points = parseItems(checklist.points, parseChecklistPoint);
  const grade = enumValue(["A", "B", "C"] as const, checklist.grade);
  if (
    !listing ||
    (expectedId !== undefined && listing.id.toLowerCase() !== expectedId.toLowerCase()) ||
    !photoIds ||
    photoIds.length !== listing.photoCount ||
    new Set(photoIds).size !== photoIds.length ||
    (photoIds[0] ?? null) !== listing.coverPhotoId ||
    !points?.length ||
    new Set(points.map((point) => point.code)).size !== points.length ||
    typeof checklist.catalogVersion !== "string" ||
    !checklist.catalogVersion.trim() ||
    !timestamp(checklist.signedAt) ||
    (checklist.grade !== null && !grade) ||
    typeof value.description !== "string" ||
    !value.description.trim() ||
    value.description.length > 2000 ||
    !money(value.roadTax) ||
    typeof value.sstRate !== "number" ||
    !Number.isFinite(value.sstRate) ||
    value.sstRate < 0 ||
    value.sstRate > 1 ||
    !money(price.vehiclePrice) ||
    !money(price.processingFee) ||
    !money(price.roadTax) ||
    !money(price.serviceTax) ||
    !money(price.promoDiscount) ||
    !money(price.total) ||
    price.vehiclePrice !== listing.vehiclePrice ||
    price.roadTax !== value.roadTax ||
    price.promoDiscount !== 0 ||
    Math.round(price.total * 100) !==
      Math.round(price.vehiclePrice * 100) +
        Math.round(price.processingFee * 100) +
        Math.round(price.roadTax * 100) +
        Math.round(price.serviceTax * 100)
  ) {
    return null;
  }
  return {
    listing,
    roadTax: value.roadTax,
    description: value.description,
    photoIds,
    sstRate: value.sstRate,
    price: {
      vehiclePrice: price.vehiclePrice,
      processingFee: price.processingFee,
      roadTax: price.roadTax,
      serviceTax: price.serviceTax,
      promoDiscount: price.promoDiscount,
      total: price.total,
    },
    checklist: {
      catalogVersion: checklist.catalogVersion,
      grade,
      signedAt: checklist.signedAt,
      points,
    },
  };
}
