import { isPositiveInteger, isRecord, isUtcInstant, isUuid } from "@/lib/auth/contracts";
import { parseSaleSnapshot, nullable, text } from "./parse";
import type { SaleSnapshot } from "./contracts";

export interface SaleReviewedVehicle {
  id: string;
  registrationPlate: string;
  vin: string;
  make: string;
  model: string;
  trim: string;
  manufactureYear: number;
  mileageKilometres: number;
}

export interface SaleSettlementReview {
  buyerName: string;
  buyerNric: string;
  buyerAddress: string;
  vehicle: SaleReviewedVehicle;
}

// After the retention period the buyer's name, NRIC and address are removed from an issued document's copy.
export interface SaleDocumentReview {
  buyerName: string | null;
  buyerNric: string | null;
  buyerAddress: string | null;
  vehicle: SaleReviewedVehicle;
}

export const buyerDetailsRemoved = (review: SaleDocumentReview) => review.buyerName === null;

export const documentLabels = {
  PurchaseOrder: "Purchase order",
  PaymentReceipt: "Payment receipt",
  HandoverNote: "Handover note",
} as const;

export interface SaleDocumentCorrectionReceipt {
  operationId: string;
  saleId: string;
  originalDocumentId: string;
  replacementDocumentId: string;
  replacementNumber: string;
  actorId: string;
  recordedAt: string;
  reason: string | null;
}

export interface SaleDocument {
  id: string;
  kind: keyof typeof documentLabels;
  number: string;
  issuedAt: string;
  contents: {
    customerId: string;
    branchId: string;
    branchName: string;
    branchAddress: string;
    reservationNumber: string;
    review: SaleDocumentReview;
    sale: SaleSnapshot;
  };
  replacesDocumentId: string | null;
  replacementDocumentId: string | null;
  correction: SaleDocumentCorrectionReceipt | null;
}

function validVehicle(vehicle: Record<string, unknown>) {
  return (
    isUuid(vehicle.id) &&
    [vehicle.registrationPlate, vehicle.vin, vehicle.make, vehicle.model].every(text) &&
    typeof vehicle.trim === "string" &&
    isPositiveInteger(vehicle.manufactureYear) &&
    Number.isSafeInteger(vehicle.mileageKilometres) &&
    Number(vehicle.mileageKilometres) >= 0
  );
}

export function parseSettlementReview(value: unknown): SaleSettlementReview | null {
  if (!isRecord(value) || !isRecord(value.vehicle)) {
    return null;
  }
  if (
    !text(value.buyerName) ||
    typeof value.buyerNric !== "string" ||
    !/^\d{12}$/.test(value.buyerNric) ||
    !text(value.buyerAddress) ||
    !validVehicle(value.vehicle)
  ) {
    return null;
  }
  return value as unknown as SaleSettlementReview;
}

// An issued document's review is complete, or has all three buyer fields removed together.
export function parseDocumentReview(value: unknown): SaleDocumentReview | null {
  if (!isRecord(value) || !isRecord(value.vehicle)) {
    return null;
  }
  const removed = [value.buyerName, value.buyerNric, value.buyerAddress].every(
    (field) => field === null || field === undefined,
  );
  if (!removed) {
    return parseSettlementReview(value);
  }
  if (!validVehicle(value.vehicle)) {
    return null;
  }
  return {
    buyerName: null,
    buyerNric: null,
    buyerAddress: null,
    vehicle: value.vehicle as unknown as SaleReviewedVehicle,
  };
}

export function parseDocumentCorrection(value: unknown): SaleDocumentCorrectionReceipt | null {
  if (
    !isRecord(value) ||
    ![
      value.operationId,
      value.saleId,
      value.originalDocumentId,
      value.replacementDocumentId,
      value.actorId,
    ].every(isUuid) ||
    !text(value.replacementNumber) ||
    !isUtcInstant(value.recordedAt) ||
    !(value.reason === null || value.reason === undefined || text(value.reason)) ||
    value.originalDocumentId === value.replacementDocumentId
  ) {
    return null;
  }
  return { ...value, reason: value.reason ?? null } as unknown as SaleDocumentCorrectionReceipt;
}

export function parseSaleDocument(value: unknown): SaleDocument | null {
  if (!isRecord(value) || !isRecord(value.contents)) {
    return null;
  }
  const contents = value.contents;
  const review = parseDocumentReview(contents.review);
  const sale = parseSaleSnapshot(contents.sale);
  const correction = value.correction === null ? null : parseDocumentCorrection(value.correction);
  if (
    !isUuid(value.id) ||
    typeof value.kind !== "string" ||
    !Object.hasOwn(documentLabels, value.kind) ||
    !text(value.number) ||
    !isUtcInstant(value.issuedAt) ||
    !isUuid(contents.customerId) ||
    !isUuid(contents.branchId) ||
    ![contents.branchName, contents.branchAddress, contents.reservationNumber].every(text) ||
    !review ||
    !sale ||
    !["Settled", "Delivered"].includes(sale.status) ||
    !nullable(isUuid)(value.replacesDocumentId) ||
    !nullable(isUuid)(value.replacementDocumentId) ||
    value.id === value.replacesDocumentId ||
    value.id === value.replacementDocumentId ||
    (value.correction !== null &&
      (!correction ||
        !(
          (correction.replacementDocumentId === value.id &&
            correction.originalDocumentId === value.replacesDocumentId) ||
          (correction.originalDocumentId === value.id &&
            correction.replacementDocumentId === value.replacementDocumentId)
        )))
  ) {
    return null;
  }
  return { ...value, contents: { ...contents, review, sale }, correction } as SaleDocument;
}

export function parseSaleDocuments(value: unknown): SaleDocument[] | null {
  if (!Array.isArray(value) || value.length > 3) {
    return null;
  }
  const documents = value.map(parseSaleDocument);
  if (documents.some((document) => !document || document.replacementDocumentId !== null)) {
    return null;
  }
  const parsed = documents as SaleDocument[];
  return new Set(parsed.map((document) => document.kind)).size === parsed.length &&
    new Set(parsed.map((document) => document.id)).size === parsed.length
    ? parsed
    : null;
}

export const saleDocumentsPath = (saleId: string) => `/api/sales/${saleId}/documents`;
export const saleDocumentPdf = (saleId: string, documentId: string) =>
  `${saleDocumentsPath(saleId)}/${documentId}/pdf`;
