import { isRecord, isUuid } from "@/lib/auth/contracts";
import { boundedText, parseHandoverEvidence, parsePaymentEvidence } from "./completion-evidence";
import { parseSettlementReview } from "./documents";

export function shapeSettlement(body: Record<string, unknown>) {
  const payment = parsePaymentEvidence(body.payment);
  const review = parseSettlementReview(body.review);
  if (
    !exactKeys(body, ["operationId", "expectedVersion", "payment", "review"]) ||
    !exactKeys(body.payment, [
      "method",
      "receivedAmount",
      "paymentReference",
      "paymentDate",
      "bank",
      "financingAmount",
      "louReference",
      "louVerified",
      "zeroBalanceConfirmed",
    ]) ||
    !exactKeys(body.review, ["buyerName", "buyerNric", "buyerAddress", "vehicle"]) ||
    !exactKeys(review?.vehicle, [
      "id",
      "registrationPlate",
      "vin",
      "make",
      "model",
      "trim",
      "manufactureYear",
      "mileageKilometres",
    ]) ||
    !payment ||
    !review ||
    !boundedText(review.buyerName, 200) ||
    !boundedText(review.buyerAddress, 1000)
  ) {
    return null;
  }
  return { payment, review };
}

export function shapeHandover(body: Record<string, unknown>) {
  if (
    !exactKeys(body, ["operationId", "expectedVersion", "evidence"]) ||
    !exactKeys(body.evidence, [
      "odometerKilometres",
      "keysDelivered",
      "customerAcknowledged",
      "ownershipTransferCompleted",
      "insuranceConfirmed",
    ])
  ) {
    return null;
  }
  const evidence = parseHandoverEvidence(body.evidence);
  return evidence ? { evidence } : null;
}

export function shapeDocumentCorrection(body: Record<string, unknown>) {
  if (
    !isUuid(body.documentId) ||
    !boundedText(body.buyerName, 200) ||
    !boundedText(body.buyerAddress, 1000) ||
    !boundedText(body.reason, 1000)
  ) {
    return null;
  }
  return {
    documentId: body.documentId,
    buyerName: body.buyerName.trim(),
    buyerAddress: body.buyerAddress.trim(),
    reason: body.reason.trim(),
  };
}

export function exactKeys(value: unknown, keys: string[]) {
  return isRecord(value) && Object.keys(value).every((key) => keys.includes(key));
}
