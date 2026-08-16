import { expect, test } from "@playwright/test";
import {
  accountSectionPath,
  sectionFromPathname,
  sectionFromSegments,
} from "@/lib/profile/sections";

test("account sections are kebab-case paths and the first section is the workspace root", () => {
  expect(accountSectionPath("reservations")).toBe("/account");
  expect(accountSectionPath("appointments")).toBe("/account/appointments");
  expect(accountSectionPath("test-drives")).toBe("/account/test-drives");
  expect(accountSectionPath("details")).toBe("/account/details");
});

test("only the root and one named section resolve and everything else is not found", () => {
  expect(sectionFromSegments(undefined)).toBe("reservations");
  expect(sectionFromSegments([])).toBe("reservations");
  expect(sectionFromSegments(["test-drives"])).toBe("test-drives");
  expect(sectionFromSegments(["reservations"])).toBeNull();
  expect(sectionFromSegments(["testDrives"])).toBeNull();
  expect(sectionFromSegments(["details", "extra"])).toBeNull();
  expect(sectionFromSegments(["account-details"])).toBeNull();
});

test("the browser pathname maps back to its section and falls back to the root", () => {
  expect(sectionFromPathname("/account")).toBe("reservations");
  expect(sectionFromPathname("/account/appointments")).toBe("appointments");
  expect(sectionFromPathname("/account/unknown")).toBe("reservations");
});
