import { expect, test } from "@playwright/test";
import { parsePurchaseGuide } from "@/lib/guide/purchase-guide";

const guide = { processingFee: 500, sstRate: 0.08, reservationHoldHours: 48, warranties: [] };

test("purchase guide carries the live reservation hold length within the administrator bounds", () => {
  expect(parsePurchaseGuide(guide)?.reservationHoldHours).toBe(48);
  expect(parsePurchaseGuide({ ...guide, reservationHoldHours: 1 })).not.toBeNull();
  expect(parsePurchaseGuide({ ...guide, reservationHoldHours: 168 })).not.toBeNull();
  for (const reservationHoldHours of [0, 169, 1.5, "48", null]) {
    expect(parsePurchaseGuide({ ...guide, reservationHoldHours })).toBeNull();
  }
  expect(parsePurchaseGuide({ processingFee: 500, sstRate: 0.08, warranties: [] })).toBeNull();
});
