import { expect, test } from "@playwright/test";
import { settingsLocation, settingsSelectionRoute } from "../../src/lib/settings/settings-location";

const id = "5b0f3a1e-6c1d-4f4e-9d0a-2f4f6f1a7c11";

test("settings addresses name the section and record in the path and omit defaults", () => {
  expect(settingsLocation("/dashboard/settings", "")?.href).toBe("/dashboard/settings");
  expect(settingsLocation("/dashboard/settings/promos", "")?.href).toBe(
    "/dashboard/settings/promos",
  );
  expect(
    settingsLocation(
      `/dashboard/settings/promos/${id}`,
      "status=expired&page=2&sort=code&direction=desc",
    )?.href,
  ).toBe(`/dashboard/settings/promos/${id}?page=2&status=expired&sort=code&direction=desc`);
  expect(settingsLocation(`/dashboard/settings/warranties/${id}`, "pageSize=50")?.href).toBe(
    `/dashboard/settings/warranties/${id}?pageSize=50`,
  );
});

test("legacy tokens, sections and extra segments are rejected", () => {
  expect(settingsLocation("/dashboard/settings/promos", "status=Expired")?.href).toBe(
    "/dashboard/settings/promos",
  );
  expect(settingsLocation("/dashboard/settings/warranties", "status=expired")?.href).toBe(
    "/dashboard/settings/warranties",
  );
  expect(settingsLocation("/dashboard/settings", "section=promos&promo=" + id)?.href).toBe(
    "/dashboard/settings",
  );
  expect(settingsSelectionRoute("settings")).toBeNull();
  expect(settingsSelectionRoute("promos", ["not-an-id"])).toBeNull();
  expect(settingsSelectionRoute("promos", [id, "extra"])).toBeNull();
  expect(settingsSelectionRoute("rules")).toBeNull();
});
