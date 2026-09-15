import { expect, test } from "@playwright/test";
import { parseSaleSnapshot } from "@/lib/sales/parse";
import { parsePaymentEvidence, isPaymentDate } from "@/lib/sales/completion-evidence";
import {
  shapeHandover,
  shapeSettlement,
  shapeDocumentCorrection,
} from "@/lib/sales/completion-input";
import { parseSaleDocument, parseSaleDocuments } from "@/lib/sales/documents";

const id = "4f1d2c3b-5a6e-4b7c-8d9e-0f1a2b3c4d5e";
const other = "8a3b0c1e-2f44-4f3b-9c55-2a7d1e0f9b11";
const time = "2026-11-02T02:00:00Z";
const payment = {
  method: 1,
  receivedAmount: 50000,
  paymentReference: "FT-29470",
  paymentDate: "2026-11-02",
  bank: null,
  financingAmount: 0,
  louReference: null,
  louVerified: false,
  zeroBalanceConfirmed: false,
};
const review = {
  buyerName: "Tan Hui Min",
  buyerNric: "920823145268",
  buyerAddress: "16 Jalan SS 2/44, Petaling Jaya",
  vehicle: {
    id,
    registrationPlate: "BRS 4892",
    vin: "PL1BT000000000001",
    make: "Proton",
    model: "Saga",
    trim: "Standard",
    manufactureYear: 2020,
    mileageKilometres: 90000,
  },
};
const snapshot = {
  status: "Settled",
  agentId: id,
  claimedAt: time,
  dueAt: null,
  pausedSeconds: null,
  orderNumber: "VPO-2026-000001",
  promoCode: null,
  basePrice: 50000,
  roadTax: 90,
  processingFee: 500,
  sstRate: 0.08,
  serviceTax: 40,
  promoDiscount: 0,
  negotiatedDiscount: 130,
  warrantyPackageId: null,
  warrantyName: null,
  warrantyPrice: 0,
  depositCredit: 500,
  tradeInAppointmentId: null,
  tradeInLabel: null,
  tradeInAllowance: 0,
  balancePayable: 50000,
  submittedAt: time,
  discountLimitAtSubmit: 500,
  decidedById: id,
  decidedAt: time,
  closedAt: null,
  version: 4,
  paymentEvidence: payment,
  settledAt: time,
  acquiredVehicleId: null,
  handoverEvidence: null,
  deliveredAt: null,
};
const document = {
  id,
  kind: "PurchaseOrder",
  number: "VPO-2026-000001",
  issuedAt: time,
  contents: {
    customerId: id,
    branchId: other,
    branchName: "ApexAuto Petaling Jaya",
    branchAddress: "22 Jalan SS 2/55",
    reservationNumber: "VRS-2026-000001",
    review,
    sale: snapshot,
  },
  replacesDocumentId: null,
  replacementDocumentId: null,
  correction: null,
};

test("completion snapshots normalize the actual numeric payment enum and reject missing milestone evidence", () => {
  expect(parseSaleSnapshot(snapshot)?.paymentEvidence?.method).toBe("BankTransfer");
  expect(parseSaleSnapshot({ ...snapshot, paymentEvidence: null })).toBeNull();
  expect(parseSaleSnapshot({ ...snapshot, status: "Delivered" })).toBeNull();
  expect(parsePaymentEvidence({ ...payment, method: 4 })).toBeNull();
  expect(parsePaymentEvidence({ ...payment, receivedAmount: 1.001 })).toBeNull();
  expect(parsePaymentEvidence({ ...payment, bank: "bank\nname" })).toBeNull();
  expect(isPaymentDate("2026-02-30")).toBe(false);
  expect(isPaymentDate("2028-02-29")).toBe(true);
});

test("settlement shapes retain reviewed identity and leave full-balance authority to the backend", () => {
  const request = { operationId: id, expectedVersion: 4, payment, review };
  expect(shapeSettlement(request)?.review.vehicle.id).toBe(id);
  expect(
    shapeSettlement({ ...request, payment: { ...payment, receivedAmount: 1 } }),
  ).not.toBeNull();
  for (const change of [
    { financialOverride: true },
    { review: { ...review, buyerNric: "123" } },
    { review: { ...review, buyerAddress: "A".repeat(1001) } },
    { review: { ...review, customerId: other } },
    { payment: { ...payment, paid: true } },
  ]) {
    expect(shapeSettlement({ ...request, ...change })).toBeNull();
  }
  expect(
    shapeSettlement({ ...request, review: { ...review, buyerAddress: "A".repeat(1000) } }),
  ).not.toBeNull();
  const evidence = {
    odometerKilometres: 90000,
    keysDelivered: 0,
    customerAcknowledged: true,
    ownershipTransferCompleted: true,
    insuranceConfirmed: true,
  };
  expect(
    shapeHandover({ operationId: id, expectedVersion: 4, evidence })?.evidence.keysDelivered,
  ).toBe(0);
  expect(shapeHandover({ evidence: { ...evidence, keysDelivered: undefined } })).toBeNull();
  expect(shapeHandover({ evidence: { ...evidence, odometerKilometres: -1 } })).toBeNull();
  expect(
    shapeDocumentCorrection({
      documentId: id,
      buyerName: " Tan Hui Min ",
      buyerAddress: "16 Jalan SS 2/44",
      reason: "Address spelling",
    })?.buyerName,
  ).toBe("Tan Hui Min");
});

test("current documents have unique kinds and older correction bodies retain the correct chain direction", () => {
  expect(parseSaleDocuments([document])?.[0].contents.sale.paymentEvidence?.method).toBe(
    "BankTransfer",
  );
  expect(parseSaleDocuments([document, { ...document, id: other }])).toBeNull();
  expect(parseSaleDocuments([{ ...document, replacementDocumentId: other }])).toBeNull();
  const correction = {
    operationId: other,
    saleId: id,
    originalDocumentId: id,
    replacementDocumentId: other,
    replacementNumber: "VPO-2026-000002",
    actorId: id,
    recordedAt: time,
    reason: "Buyer address spelling",
  };
  expect(
    parseSaleDocument({ ...document, replacementDocumentId: other, correction })
      ?.replacementDocumentId,
  ).toBe(other);
  expect(
    parseSaleDocument({ ...document, id: other, replacesDocumentId: id, correction })
      ?.replacesDocumentId,
  ).toBe(id);
  expect(parseSaleDocument({ ...document, correction })).toBeNull();
  expect(
    parseSaleDocument({ ...document, contents: { ...document.contents, customerId: "invalid" } }),
  ).toBeNull();
});

test("a document whose buyer details were removed by retention still parses and cannot be half removed", () => {
  const stripped = { vehicle: review.vehicle };
  const removed = parseSaleDocument({
    ...document,
    contents: { ...document.contents, review: stripped },
  });
  expect(removed?.contents.review.buyerName).toBeNull();
  expect(removed?.contents.review.vehicle.id).toBe(id);
  const explicitNulls = {
    ...stripped,
    buyerName: null,
    buyerNric: null,
    buyerAddress: null,
  };
  expect(
    parseSaleDocument({ ...document, contents: { ...document.contents, review: explicitNulls } }),
  ).not.toBeNull();
  // One surviving buyer field means the removal was partial, which the database never produces.
  expect(
    parseSaleDocument({
      ...document,
      contents: { ...document.contents, review: { ...stripped, buyerName: "Tan Hui Min" } },
    }),
  ).toBeNull();
  // A correction whose reason was removed still reads as a correction.
  const correction = {
    operationId: id,
    saleId: id,
    originalDocumentId: id,
    replacementDocumentId: other,
    replacementNumber: "VPO-2026-000002",
    actorId: id,
    recordedAt: time,
    reason: null,
  };
  expect(
    parseSaleDocument({ ...document, replacementDocumentId: other, correction })?.correction
      ?.reason,
  ).toBeNull();
});
