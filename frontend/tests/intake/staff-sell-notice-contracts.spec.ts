import { expect, test } from "@playwright/test";
import type { CurrentActor } from "@/lib/auth/actor";
import { staffSellNotice } from "@/lib/intake/staff-sell-notice";

const branchId = "5f3c1f58-1b0e-4f0e-9f6a-0f4f6f2f9a11";

function actor(role: CurrentActor["role"], branch: string | null): CurrentActor {
  return { userId: "1c7a4f9e-5b2d-4e63-8a41-3d9b7e0c2a55", role, branchId: branch };
}

test("customers need no staff notice on the sell page", () => {
  expect(staffSellNotice(actor("Customer", null))).toBeNull();
});

test("roles with intake access are sent to the intake workspace", () => {
  for (const role of ["SalesAgent", "BranchManager"] as const) {
    const notice = staffSellNotice(actor(role, branchId));
    expect(notice?.href).toBe("/dashboard/intake");
    expect(notice?.assisted).toBe(true);
  }
  expect(staffSellNotice(actor("GeneralManager", null))?.href).toBe("/dashboard/intake");
  expect(staffSellNotice(actor("GeneralManager", null))?.assisted).toBe(true);
});

test("other staff roles are sent to the workspace their role may use", () => {
  expect(staffSellNotice(actor("WorkshopTechnician", branchId))?.href).toBe("/dashboard/workshop");
  expect(staffSellNotice(actor("Administrator", null))?.href).toBe("/dashboard/accounts");
  expect(staffSellNotice(actor("WorkshopTechnician", branchId))?.assisted).toBe(false);
  expect(staffSellNotice(actor("Administrator", null))?.assisted).toBe(false);
});
