import { expect, test } from "@playwright/test";
import {
  parseStaffAccountPage,
  staffAccountParams,
  staffAccountQuery,
} from "@/lib/accounts/staff-accounts";

const id = "9b2f6f0e-4c0e-4f6e-8b0e-0a8f5d2c1e11";
const actor = { userId: id, role: "Administrator", branchId: null, emailVerified: true };
const row = {
  id,
  name: "Aina Sofia",
  email: "aina.sofia@example.invalid",
  mobile: "0129876543",
  role: "SalesAgent",
  branchId: null,
  branchName: null,
  status: "Active",
  pictureId: null,
  invitationExpiresAt: null,
};
const page = {
  actor,
  items: [row],
  total: 1,
  page: 1,
  pageSize: 25,
  counts: { all: 1, active: 1, invited: 0, suspended: 0, deactivated: 0 },
};

test("staff account pages keep only recognised states, counts and unique rows", () => {
  expect(parseStaffAccountPage(page)?.items).toHaveLength(1);
  expect(parseStaffAccountPage({ ...page, items: [{ ...row, status: "Suspended?" }] })).toBeNull();
  expect(parseStaffAccountPage({ ...page, items: [row, row], total: 2 })).toBeNull();
  expect(parseStaffAccountPage({ ...page, counts: { all: 1 } })).toBeNull();
  expect(parseStaffAccountPage({ ...page, actor: null })).toBeNull();
});

test("staff account parameters accept only known tabs and roles and leave defaults off the query", () => {
  const unknown = staffAccountParams({ tab: "Everything", role: "Customer", branchId: "nope" });
  expect(unknown.filters).toMatchObject({ tab: "All", role: "", branchId: "" });
  const known = staffAccountParams({ tab: "Invited", role: "SalesAgent", search: " aina " });
  expect(known.filters).toMatchObject({ tab: "Invited", role: "SalesAgent" });
  const query = new URLSearchParams(staffAccountQuery(known.filters, 2));
  expect([query.get("tab"), query.get("role"), query.get("page")]).toEqual([
    "Invited",
    "SalesAgent",
    "2",
  ]);
  expect(new URLSearchParams(staffAccountQuery(unknown.filters)).has("tab")).toBe(false);
});
