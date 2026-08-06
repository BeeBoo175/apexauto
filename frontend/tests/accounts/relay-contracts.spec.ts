import { expect, test } from "@playwright/test";
import { staffAccountConflict } from "@/lib/accounts/account-administration";
import { staffInvitationConflict } from "@/lib/accounts/staff-invitations";
import { staffActivationConflict } from "@/lib/auth/staff-invitation";
import { echoesPage } from "@/lib/pagination";
import { withQuery } from "@/lib/query";

const page = { page: 2, pageSize: 10, items: [] as string[] };

test("invitation conflict codes keep administrator and public activation guidance distinct", () => {
  expect(staffInvitationConflict("email-taken")).toContain("pending invitation");
  expect(staffActivationConflict("email-taken")).toContain("Ask an administrator");
  expect(staffInvitationConflict("username-taken")).toBe("That username is taken.");
  expect(staffActivationConflict("username-taken")).toContain("Choose another");
  for (const code of ["unavailable", "unknown", "toString", "__proto__", null, 409, {}]) {
    expect(staffInvitationConflict(code)).toContain("Refresh the list");
    expect(staffActivationConflict(code)).toContain("new one");
  }
});

test("a relayed page answers only the page and size that were requested", () => {
  expect(echoesPage(page, new URLSearchParams({ page: "2", pageSize: "10" }))).toBe(page);
  expect(echoesPage(page, new URLSearchParams({ page: "1", pageSize: "10" }))).toBeNull();
  expect(echoesPage(page, new URLSearchParams({ page: "2" }))).toBeNull();
  expect(echoesPage({ ...page, page: 1, pageSize: 25 }, new URLSearchParams())).not.toBeNull();
  expect(echoesPage(null, new URLSearchParams())).toBeNull();
});

test("a forwarded path carries only a non-empty reviewed query", () => {
  expect(withQuery("/api/staff-accounts", new URLSearchParams())).toBe("/api/staff-accounts");
  expect(withQuery("/api/staff-accounts", new URLSearchParams({ view: "active" }))).toBe(
    "/api/staff-accounts?view=active",
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
