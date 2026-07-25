import { expect, test } from "@playwright/test";
import { staffAccountConflict } from "@/lib/accounts/account-administration";
import { echoesPage } from "@/lib/pagination";
import { withQuery } from "@/lib/query";

const page = { page: 2, pageSize: 10, items: [] as string[] };

test("a relayed page answers only the page and size that were requested", () => {
  expect(echoesPage(page, new URLSearchParams({ page: "2", pageSize: "10" }))).toBe(page);
  expect(echoesPage(page, new URLSearchParams({ page: "1", pageSize: "10" }))).toBeNull();
  expect(echoesPage(page, new URLSearchParams({ page: "2" }))).toBeNull();
  expect(echoesPage({ ...page, page: 1, pageSize: 25 }, new URLSearchParams())).not.toBeNull();
  expect(echoesPage(null, new URLSearchParams())).toBeNull();
});

test("a forwarded path carries only a non-empty reviewed query", () => {
  expect(withQuery("/api/staff-accounts", new URLSearchParams())).toBe("/api/staff-accounts");
  expect(withQuery("/api/staff-accounts", new URLSearchParams({ tab: "active" }))).toBe(
    "/api/staff-accounts?tab=active",
  );
});

test("a blocked staff account conflict names open work and others keep their own command text", () => {
  const blocked = staffAccountConflict("deactivations", "blocked");
  expect(blocked).toContain("open work");
  expect(staffAccountConflict("transfers", "blocked")).toBe(blocked);
  expect(staffAccountConflict("deactivations", "conflict")).toContain("last active Administrator");
  expect(staffAccountConflict("deactivations", null)).toContain("last active Administrator");
  expect(staffAccountConflict("reactivations", undefined)).toContain("not deactivated");
  expect(staffAccountConflict("transfers", "Other")).toContain("active branch");
});
