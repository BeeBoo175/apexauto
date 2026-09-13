import { expect, test } from "@playwright/test";
import {
  parseRetentionSettings,
  readRetentionDraft,
  retentionDraftFrom,
  retentionShortened,
  retentionUnchanged,
} from "../../src/lib/settings/retention";
import { settingsLocation, settingsSelectionRoute } from "../../src/lib/settings/settings-location";

const bounds = {
  minimumDays: 7,
  maximumDays: 365,
  minimumBusinessYears: 1,
  maximumBusinessYears: 10,
};

const wire = {
  securityArtifactDays: 30,
  mailDetailDays: 30,
  invitationDays: 30,
  businessEvidenceYears: 7,
  updatedAtUtc: "2026-11-16T01:00:00.000+00:00",
  version: 3,
  bounds,
};

test("the retention contract accepts the server shape and rejects incomplete ones", () => {
  const settings = parseRetentionSettings(wire);
  expect(settings?.businessEvidenceYears).toBe(7);
  expect(parseRetentionSettings({ ...wire, version: 0 })).toBeNull();
  expect(parseRetentionSettings({ ...wire, bounds: { ...bounds, maximumDays: 3 } })).toBeNull();
  expect(parseRetentionSettings({ ...wire, mailDetailDays: "30" })).toBeNull();
  expect(parseRetentionSettings({ ...wire, bounds: undefined })).toBeNull();
});

test("a draft is checked against the server bounds and shortening is detected", () => {
  const settings = parseRetentionSettings(wire)!;
  const draft = retentionDraftFrom(settings);

  const same = readRetentionDraft(draft, bounds);
  expect(same.values && retentionUnchanged(same.values, settings)).toBe(true);
  expect(same.values && retentionShortened(same.values, settings)).toBe(false);

  const extended = readRetentionDraft({ ...draft, securityArtifactDays: "60" }, bounds);
  expect(extended.values && retentionShortened(extended.values, settings)).toBe(false);

  const shortened = readRetentionDraft({ ...draft, businessEvidenceYears: "5" }, bounds);
  expect(shortened.values && retentionShortened(shortened.values, settings)).toBe(true);

  const invalid = readRetentionDraft(
    { ...draft, securityArtifactDays: "6", mailDetailDays: "abc", businessEvidenceYears: "11" },
    bounds,
  );
  expect(invalid.values).toBeNull();
  expect(Object.keys(invalid.errors).sort()).toEqual([
    "businessEvidenceYears",
    "mailDetailDays",
    "securityArtifactDays",
  ]);
});

test("retention has its own address with no record segment or list query", () => {
  expect(settingsLocation("/dashboard/settings/retention", "")?.href).toBe(
    "/dashboard/settings/retention",
  );
  expect(settingsLocation("/dashboard/settings/retention", "page=2&view=expired")?.href).toBe(
    "/dashboard/settings/retention",
  );
  expect(settingsSelectionRoute("retention")).toEqual({ section: "retention", id: "" });
  expect(settingsSelectionRoute("retention", ["5b0f3a1e-6c1d-4f4e-9d0a-2f4f6f1a7c11"])).toBeNull();
});
