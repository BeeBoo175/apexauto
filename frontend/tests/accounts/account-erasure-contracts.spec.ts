import { expect, test } from "@playwright/test";
import {
  erasureConflict,
  erasurePath,
  erasurePreviewPath,
  parseErasurePreview,
  parseErasureRequest,
} from "@/lib/accounts/account-erasure";

const id = "5c0c5e34-6a8e-4b8e-a1b5-5d6f8f6f0a11";
const retained = [{ name: "Work history", detail: "Kept by number.", endsWhen: "Seven years" }];

test("customer and staff erasure use their own routes and share the customer preview shape", () => {
  expect(erasurePath("customer", id)).toBe(`/api/accounts/${id}/erasures`);
  expect(erasurePath("staff", id)).toBe(`/api/staff-accounts/${id}/erasures`);
  expect(erasurePreviewPath("staff", id)).toBe(`/api/staff-accounts/${id}/blockers`);
});

test("a staff preview names the managers who resolve each blocker", () => {
  const staff = {
    blockers: [
      {
        type: "Sale",
        reference: "AB12CD34",
        status: "Approved",
        branchName: "Glenmarie",
        managers: "Siti Aminah",
      },
      {
        type: "Test drive",
        reference: "EF56AB78",
        status: "Booked",
        branchName: "Bayan Lepas",
        managers: "No active branch manager",
      },
    ],
    retained,
  };
  const preview = parseErasurePreview("staff", staff);
  expect(preview?.blockers[0].resolution).toContain("Siti Aminah");
  expect(preview?.blockers[1].resolution).toContain("assign one first");
  expect(parseErasurePreview("staff", { blockers: [] })).toBeNull();
});

test("the erasure body needs an operation id, a trimmed reason and a password within the limits", () => {
  const body = { operationId: id, reason: "  Subject request  ", password: "Password123!" };
  expect(parseErasureRequest(body)).toEqual({ ...body, reason: "Subject request" });
  expect(parseErasureRequest({ ...body, reason: "   " })).toBeNull();
  expect(parseErasureRequest({ ...body, reason: "x".repeat(1001) })).toBeNull();
  expect(parseErasureRequest({ ...body, password: "" })).toBeNull();
  expect(parseErasureRequest({ ...body, operationId: "nope" })).toBeNull();
});

test("only documented conflict codes map to a message", () => {
  expect(erasureConflict("wrong-password")?.message).toContain("not correct");
  expect(erasureConflict("protected")?.message).toContain("last active Administrator");
  expect(erasureConflict("surprise")).toBeUndefined();
});
