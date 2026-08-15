import { expect, test } from "@playwright/test";
import { matchesDepositRequest, parseDepositRequestReceipt } from "@/lib/vehicles/deposit-request";

const ids = [
  "11111111-1111-4111-8111-111111111111",
  "22222222-2222-4222-8222-222222222222",
  "33333333-3333-4333-8333-333333333333",
];

const receipt = {
  id: ids[0],
  vehicleId: ids[1],
  publicationId: ids[2],
  promoId: null,
  createdAt: "2026-11-12T10:00:00Z",
  expiresAt: "2026-11-13T10:00:00Z",
};

test("deposit request receipt holds the car for exactly twenty-four hours", () => {
  expect(parseDepositRequestReceipt(receipt)?.id).toBe(ids[0]);
  expect(parseDepositRequestReceipt({ ...receipt, expiresAt: "2026-11-14T10:00:00Z" })).toBeNull();
  expect(parseDepositRequestReceipt({ ...receipt, vehicleId: "not-a-uuid" })).toBeNull();
  expect(parseDepositRequestReceipt({ ...receipt, promoId: "not-a-uuid" })).toBeNull();
  expect(parseDepositRequestReceipt(null)).toBeNull();
});

test("deposit request receipt must answer the captured request for the same car", () => {
  const parsed = parseDepositRequestReceipt(receipt)!;
  const body = { operationId: ids[0], publicationId: ids[2], promoId: null };
  expect(matchesDepositRequest(parsed, body, ids[1])).toBe(true);
  expect(matchesDepositRequest(parsed, { ...body, operationId: ids[1] }, ids[1])).toBe(false);
  expect(matchesDepositRequest(parsed, body, ids[0])).toBe(false);
  expect(matchesDepositRequest(parsed, { ...body, promoId: ids[1] }, ids[1])).toBe(false);
});
