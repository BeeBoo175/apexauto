import { test, expect } from "@playwright/test";
import {
  parseAssessment,
  parseEntry,
  parseInspectionPage,
  parsePoint,
  parseRepairPage,
} from "@/lib/inspection/parse";
import { parseMyr } from "@/lib/currency-input";
import { queueParams, queueQuery } from "@/lib/inspection/query";
import { appliedWorklistFilters } from "@/lib/staff/filter-summary";
import { checklistDraft, saveContents } from "@/lib/inspection/draft";
import type { Draft } from "@/lib/inspection/contracts";

test("inspection adapters normalize numeric API enums and reject unknown authority values", () => {
  expect(parseAssessment({ estimatedRepairCost: 0, grade: 0, isRejected: false })?.grade).toBe("A");
  expect(parseAssessment({ estimatedRepairCost: 0, grade: 2, isRejected: false })?.grade).toBe("C");
  expect(
    parseAssessment({ estimatedRepairCost: 0, grade: null, isRejected: true })?.isRejected,
  ).toBe(true);
  expect(parseAssessment({ estimatedRepairCost: 0, grade: 3, isRejected: false })).toBeNull();
  expect(parseAssessment({ estimatedRepairCost: 0, grade: 0, isRejected: true })).toBeNull();
  expect(parsePoint({ code: "FC-001", category: 0, description: "VIN identity" })?.category).toBe(
    "FrameChassis",
  );
  expect(
    parseEntry({
      pointCode: "FC-001",
      kind: 0,
      severity: 2,
      estimatedRepairCost: 0,
      notes: "Structural finding",
    })?.severity,
  ).toBe("MajorDefect");
  expect(
    parseEntry({
      pointCode: "FC-001",
      kind: 1,
      severity: null,
      estimatedRepairCost: 1,
      notes: "Absent",
    }),
  ).toBeNull();
  expect(
    parseInspectionPage({ items: [], total: 0, page: 2147483647, pageSize: 25 }),
  ).not.toBeNull();
  expect(parseRepairPage({ items: [], total: 0, page: 1, pageSize: 500 })).toBeNull();
  expect(queueParams({ page: "-1", pageSize: "50" }, "repair-requests")).toMatchObject({
    filters: { status: "All", pageSize: 50 },
    page: 1,
  });
});

test("repair queues default to all and shared pills remove only the chosen applied criterion", () => {
  const initial = queueParams({}, "repair-requests");
  expect(initial.filters.status).toBe("All");
  expect(new URLSearchParams(queueQuery(initial.filters, 1)).get("status")).toBe("All");
  const filters = {
    ...initial.filters,
    status: "Approved",
    branchId: "branch",
    from: "2026-10-01",
    to: "2026-10-22",
    search: "VCA",
  };
  const pills = appliedWorklistFilters(filters, "Glenmarie", (status) => status);
  expect(pills.map((pill) => pill.key)).toEqual(["dates", "status", "branch"]);
  expect(pills.find((pill) => pill.key === "dates")?.without).toEqual({
    ...filters,
    from: "",
    to: "",
  });
  expect(pills.find((pill) => pill.key === "status")?.without).toEqual({
    ...filters,
    status: "All",
  });
  expect(pills.find((pill) => pill.key === "branch")?.without).toEqual({
    ...filters,
    branchId: "",
  });
});

test("MYR input never silently rounds sub-cents or unsafe amounts", () => {
  expect(parseMyr("250.10")).toBe(250.1);
  expect(parseMyr("000250.1")).toBe(250.1);
  expect(parseMyr("0")).toBe(0);
  expect(parseMyr("0.01")).toBe(0.01);
  expect(parseMyr("1000000.99")).toBe(1000000.99);
  for (const value of [
    "1.001",
    "-1",
    "1e3",
    "Infinity",
    "",
    "9007199254740993.01",
    "90071992547409.92",
    "70368744177664.01",
  ]) {
    expect(parseMyr(value)).toBeNull();
  }
});

test("partial examination never becomes full attestation and Not fitted is excluded", () => {
  const draft = {
    assignmentVersion: 2,
    latestSave: null,
    catalog: [{ code: "FC-001" }, { code: "IE-001" }],
  } as Draft;
  const value = { entries: {}, examined: ["FC-001"], photos: {} };
  expect(saveContents(draft, value).body?.examinedPointCodes).toBeNull();
  expect(
    saveContents(draft, { ...value, examined: ["FC-001", "IE-001"] }).body?.examinedPointCodes,
  ).toEqual(["FC-001", "IE-001"]);
  expect(
    saveContents(draft, {
      ...value,
      entries: {
        "IE-001": {
          pointCode: "IE-001",
          kind: "NotFitted",
          severity: null,
          estimate: "0.00",
          notes: "Optional equipment absent",
        },
      },
    }).body?.examinedPointCodes,
  ).toEqual(["FC-001"]);
});

test("reopening and assignment handover require a fresh examination declaration", () => {
  const draft = {
    assignmentVersion: 2,
    reopenedAfterSaveVersion: 4,
    latestSave: { assignmentVersion: 2, version: 4, entries: [], examinedPointCodes: ["FC-001"] },
    catalog: [{ code: "FC-001" }],
  } as unknown as Draft;
  const reopened = checklistDraft(draft, []);
  expect(reopened.examined).toEqual([]);
  expect(saveContents(draft, reopened).body?.examinedPointCodes).toBeNull();
  const handover = {
    ...draft,
    assignmentVersion: 3,
    latestSave: { ...draft.latestSave!, version: 5 },
  };
  expect(checklistDraft(handover, []).examined).toEqual([]);
  const current = { ...draft, latestSave: { ...draft.latestSave!, version: 5 } };
  expect(checklistDraft(current, []).examined).toEqual(["FC-001"]);
  expect(
    saveContents(draft, { ...reopened, examined: ["FC-001"] }).body?.examinedPointCodes,
  ).toEqual(["FC-001"]);
});
