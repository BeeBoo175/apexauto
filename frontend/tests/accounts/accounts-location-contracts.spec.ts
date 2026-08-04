import { expect, test } from "@playwright/test";
import {
  accountSelectionRoute,
  accountsHref,
  accountsLocation,
  customerAccountsRules,
  staffAccountsRules,
} from "../../src/lib/accounts/accounts-location";

const id = "5b0f3a1e-6c1d-4f4e-9d0a-2f4f6f1a7c11";

test("staff accounts addresses name the account and section in the path and omit defaults", () => {
  expect(accountsLocation(staffAccountsRules, "/dashboard/accounts", "")?.href).toBe(
    "/dashboard/accounts",
  );
  expect(accountsLocation(staffAccountsRules, `/dashboard/accounts/${id}/history`, "")?.href).toBe(
    `/dashboard/accounts/${id}/history`,
  );
  expect(
    accountsLocation(staffAccountsRules, `/dashboard/accounts/${id}`, "tab=suspended&page=2")?.href,
  ).toBe(`/dashboard/accounts/${id}?tab=suspended&page=2`);
});

test("customer accounts use their own path and camelCase tabs", () => {
  const location = accountsLocation(
    customerAccountsRules,
    `/dashboard/accounts/customers/${id}`,
    "tab=unverified&pageSize=50",
  );
  expect(location?.href).toBe(`/dashboard/accounts/customers/${id}?tab=unverified&pageSize=50`);
  expect(accountsLocation(staffAccountsRules, "/dashboard/accounts/customers", "")).toBeNull();
});

test("explicit details, unknown sections, extra segments and legacy tokens are rejected", () => {
  expect(accountSelectionRoute("staff", id, ["details"])).toBeNull();
  expect(accountSelectionRoute("staff", id, ["notes"])).toBeNull();
  expect(accountSelectionRoute("staff", id, ["history", "extra"])).toBeNull();
  expect(accountSelectionRoute("customers", "not-an-id")).toBeNull();
  const legacy = accountsLocation(staffAccountsRules, "/dashboard/accounts", "tab=Invited");
  expect(legacy?.filters.tab).toBe("all");
  expect(
    accountsHref(staffAccountsRules, {
      id: "",
      section: "details",
      filters: legacy!.filters,
      page: 1,
    }),
  ).toBe("/dashboard/accounts");
});
