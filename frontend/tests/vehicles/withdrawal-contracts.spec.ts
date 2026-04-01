import { expect, test } from "@playwright/test";
import {
  listingWithdrawalReason,
  matchesListingWithdrawal,
  parseListingWithdrawal,
  parseListingWithdrawalReceipt,
} from "@/lib/vehicles/listing-withdrawal";

const ids = [
  "47cf2705-95cc-48b6-b907-64bf18dd87da",
  "2f35e1b2-602b-43dc-8c5f-4085893b0ea8",
  "e26136c6-6e1b-46c2-9bb5-c28e2f01dd8c",
];
const receipt = {
  id: ids[0],
  publicationId: ids[1],
  vehicleId: ids[1],
  branchId: ids[2],
  actorId: ids[0],
  reason: "Showroom stock review",
  recordedAt: "2026-10-27T00:00:00+00:00",
};

test("withdrawal reasons match the canonical trimmed length and control-character boundaries", () => {
  expect(listingWithdrawalReason("  Showroom stock review  ")).toBe(receipt.reason);
  expect(listingWithdrawalReason("a".repeat(1000))).toHaveLength(1000);
  for (const reason of ["", "   ", "a".repeat(1001), "Reason\n", "Reason\u007f", "\u0085Reason"]) {
    expect(listingWithdrawalReason(reason)).toBeNull();
  }
});

test("withdrawal receipts require immutable identities, a canonical reason, UTC time and success", () => {
  expect(parseListingWithdrawal({ status: 0, withdrawal: receipt })).toEqual(receipt);
  expect(parseListingWithdrawal({ status: "Succeeded", withdrawal: receipt })).toEqual(receipt);
  expect(parseListingWithdrawalReceipt({ ...receipt, privateValue: "omitted" })).toEqual(receipt);
  for (const key of ["id", "publicationId", "vehicleId", "branchId", "actorId"]) {
    expect(parseListingWithdrawalReceipt({ ...receipt, [key]: "invalid" })).toBeNull();
    expect(parseListingWithdrawalReceipt({ ...receipt, [key]: undefined })).toBeNull();
  }
  for (const reason of [null, "", "   ", " padded ", "a".repeat(1001), "Reason\n"]) {
    expect(parseListingWithdrawalReceipt({ ...receipt, reason })).toBeNull();
  }
  for (const recordedAt of [null, "invalid", "2026-10-27T00:00:00", "2026-10-27T08:00:00+08:00"]) {
    expect(parseListingWithdrawalReceipt({ ...receipt, recordedAt })).toBeNull();
  }
  for (const status of [1, "Conflict", undefined]) {
    expect(parseListingWithdrawal({ status, withdrawal: receipt })).toBeNull();
  }
  expect(parseListingWithdrawal({ status: 0, withdrawal: null })).toBeNull();
});

test("withdrawal confirmation binds the exact operation, approved publication and captured reason", () => {
  const body = {
    operationId: receipt.id,
    publicationId: receipt.publicationId,
    reason: receipt.reason,
  };
  expect(matchesListingWithdrawal(receipt, body)).toBe(true);
  expect(matchesListingWithdrawal(receipt, null)).toBe(false);
  for (const key of ["operationId", "publicationId", "reason"]) {
    expect(matchesListingWithdrawal(receipt, { ...body, [key]: ids[2] })).toBe(false);
  }
});
