import { expect, test } from "@playwright/test";
import { homeLocation } from "../../src/lib/home/home-location";

const branchId = "5b0f3a1e-6c1d-4f4e-9d0a-2f4f6f1a7c11";

test("home addresses omit defaults and keep the room view in a deterministic order", () => {
  expect(homeLocation("/dashboard/home", "", true)?.href).toBe("/dashboard/home");
  expect(
    homeLocation(
      "/dashboard/home",
      `pageSize=50&page=2&search=%20amy%20&team=workshop&branchId=${branchId}`,
      true,
    )?.href,
  ).toBe(`/dashboard/home?branchId=${branchId}&team=workshop&search=amy&page=2&pageSize=50`);
});

test("only the general manager keeps a branch and other paths are not home", () => {
  expect(homeLocation("/dashboard/home", `branchId=${branchId}`, false)?.href).toBe(
    "/dashboard/home",
  );
  expect(homeLocation("/dashboard/home", "branchId=not-an-id", true)?.href).toBe("/dashboard/home");
  expect(homeLocation("/dashboard/performance", "", true)).toBeNull();
});
