import { expect, test } from "@playwright/test";
import {
  parseRetentionSettings,
  readRetentionDraft,
  retentionDraftFrom,
  retentionShortened,
  retentionUnchanged,
} from "../../src/lib/settings/retention";
import { settingsLocation, settingsSelectionRoute } from "../../src/lib/settings/settings-location";
import {
  holdUntilInstant,
  parseRetentionHoldChange,
  parseRetentionHolds,
  validHoldDraft,
  wrapRetentionHolds,
} from "../../src/lib/settings/retention-holds";

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

test("preservation hold lists and changes parse only complete rows", () => {
  const hold = {
    saleId: "4f1d2c3b-5a6e-4b7c-8d9e-0f1a2b3c4d5e",
    reservationNumber: "RSV-2026-000001",
    until: "2030-01-31T15:59:59Z",
    reason: "Dispute under review",
  };
  expect(parseRetentionHolds({ holds: [hold] })?.holds).toHaveLength(1);
  expect(wrapRetentionHolds([hold])?.holds[0].reservationNumber).toBe("RSV-2026-000001");
  expect(parseRetentionHolds({ holds: [{ ...hold, reason: "" }] })).toBeNull();
  expect(parseRetentionHolds({ holds: [{ ...hold, saleId: "x" }] })).toBeNull();
  expect(wrapRetentionHolds({})).toBeNull();
  expect(parseRetentionHoldChange({ until: null })?.until).toBeNull();
  expect(parseRetentionHoldChange({ until: "tomorrow" })).toBeNull();
  expect(holdUntilInstant("2030-01-31")).toBe("2030-01-31T15:59:59.000Z");
  expect(validHoldDraft({ reservationNumber: "RSV-1", until: "2030-01-31", reason: " " })).toBe(
    false,
  );
  expect(
    validHoldDraft({ reservationNumber: "RSV-1", until: "2030-01-31", reason: "Dispute" }),
  ).toBe(true);
});
