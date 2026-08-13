import { expect, test } from "@playwright/test";
import {
  reachableSellerStep,
  sellerStepFromToken,
  sellerStepToken,
} from "@/lib/intake/seller-steps";

test("each stage has one address token and the first has none", () => {
  expect([0, 1, 2, 3].map(sellerStepToken)).toEqual(["", "valuation", "photos", "appointment"]);
  expect(["valuation", "photos", "appointment"].map(sellerStepFromToken)).toEqual([1, 2, 3]);
  expect(sellerStepFromToken(null)).toBe(0);
});

test("an unknown or hostile token names the first stage", () => {
  for (const token of ["", "Photos", "4", "photos ", "../appointment", "constructor"]) {
    expect(sellerStepFromToken(token)).toBe(0);
  }
});

test("entered work, not the address, decides how far the journey can go", () => {
  expect(reachableSellerStep(false, 0)).toBe(0);
  expect(reachableSellerStep(false, 4)).toBe(0);
  expect(reachableSellerStep(true, 0)).toBe(2);
  expect(reachableSellerStep(true, 3)).toBe(2);
  expect(reachableSellerStep(true, 4)).toBe(3);
});
