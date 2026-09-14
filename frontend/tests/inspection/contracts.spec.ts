import { test, expect } from "@playwright/test";
import { parseInspectionGuide } from "@/lib/guide/inspection-points";
import { evaluateInspectionOutcome, inspectionCategories } from "@/lib/guide/inspection-rules";
import { isWarrantyPlanEligible } from "@/lib/guide/warranty-plans";
import {
  parseAssessment,
  parseEntry,
  parseInspectionPage,
  parsePoint,
  parseRepairPage,
} from "@/lib/inspection/parse";
import { parseMyr } from "@/lib/currency-input";
import { queueParams, queueQuery, technicianQueueQuery } from "@/lib/inspection/query";
import {
  technicianQueueParams,
  technicianQueuePaging,
  workshopLocation,
} from "@/lib/inspection/workshop-location";
import { appliedWorklistFilters } from "@/lib/staff/filter-summary";
import { checklistDraft, saveContents } from "@/lib/inspection/draft";
import type { Draft } from "@/lib/inspection/contracts";
import { parseRevisions } from "@/lib/inspection/parse-report";
import { inspectionTimeline } from "@/lib/inspection/timeline";
import type { RepairHistory, RevisionHistory } from "@/lib/inspection/report-contracts";

test("repair milestones remain chronological with one request destination and no duplicated receipts", () => {
  const request = {
    operationId: "request",
    inspectionId: "inspection",
    branchId: "branch",
    actorId: "technician",
    signedRevisionId: "signed",
    assignmentVersion: 1,
    revision: 1,
    expectedVersion: 0,
    version: 1,
    items: [{ pointCode: "EP-004", work: "Refinish the bumper." }],
    budget: 750,
    requestedAt: "2026-10-24T08:10:00Z",
  };
  const receipt = {
    inspectionId: "inspection",
    branchId: "branch",
    actorId: "manager",
    requestId: request.operationId,
    assignmentVersion: 1,
    revision: 1,
    repairVersion: 1,
  };
  const repairs: RepairHistory = {
    inspectionId: "inspection",
    revision: 1,
    page: 1,
    pageSize: 25,
    totalCount: 1,
    items: [
      {
        request,
        approval: { ...receipt, operationId: "approval", approvedAt: "2026-10-24T08:20:00Z" },
        completion: {
          ...receipt,
          operationId: "completion",
          approvalId: "approval",
          completedAt: "2026-10-24T08:30:00Z",
          notes: "Finish checked after repair.",
        },
      },
    ],
  };
  const revisions: RevisionHistory = {
    inspectionId: "inspection",
    currentRevision: 2,
    isSigned: false,
    page: 1,
    pageSize: 25,
    totalCount: 1,
    items: [
      {
        operationId: "signed",
        revision: 1,
        saveVersion: 1,
        assignmentVersion: 1,
        technicianId: "technician",
        technicianIdentity: "Workshop technician",
        catalogVersion: "v1",
        assessment: { estimatedRepairCost: 750, grade: "B", isRejected: false },
        signedAt: "2026-10-24T08:00:00Z",
        reopening: null,
        hasRepairRequests: true,
      },
    ],
  };
  const events = inspectionTimeline(revisions, null, repairs);
  expect(events.map((event) => event.kind)).toEqual([
    "completion",
    "approval",
    "request",
    "inspection",
  ]);
  expect(events.filter((event) => event.request).map((event) => event.request?.requestId)).toEqual([
    "request",
  ]);
  expect(events[0].detail).toBe("Finish checked after repair.");
  expect(inspectionTimeline(revisions, repairs, repairs)).toEqual(events);
  expect(inspectionTimeline(null, null)).toEqual([]);
});

test("inspection history requires explicit repair availability before offering a drill-down", () => {
  const item = {
    operationId: "ce97fac6-46af-4b15-ae89-a8b21e9eca34",
    technicianId: "b2f1f72d-bc46-4b94-a742-69c4063d215e",
    revision: 1,
    saveVersion: 1,
    assignmentVersion: 1,
    technicianIdentity: "Farid Hakim",
    catalogVersion: "v1",
    signedAt: "2026-10-24T08:00:00Z",
    assessment: { estimatedRepairCost: 250, grade: "B", isRejected: false },
    reopening: null,
  };
  const history = {
    inspectionId: "d0d2a660-9284-44b0-914f-eab11a0687dd",
    currentRevision: 1,
    isSigned: true,
    totalCount: 1,
    page: 1,
    pageSize: 25,
  };
  for (const hasRepairRequests of [false, true]) {
    expect(
      parseRevisions({ ...history, items: [{ ...item, hasRepairRequests }] })?.items[0]
        .hasRepairRequests,
    ).toBe(hasRepairRequests);
  }
  expect(parseRevisions({ ...history, items: [item] })).toBeNull();
  expect(parseRevisions({ ...history, items: [{ ...item, hasRepairRequests: "yes" }] })).toBeNull();
  // The retention period removes the technician's sign-in name from a signed report; the revision stays valid.
  for (const technicianIdentity of [null, undefined]) {
    const removed = { ...item, hasRepairRequests: false, technicianIdentity };
    expect(
      parseRevisions({ ...history, items: [removed] })?.items[0].technicianIdentity,
    ).toBeNull();
  }
});

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
  expect(new URLSearchParams(queueQuery(initial.filters, 1)).get("view")).toBe("all");
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

test("workshop query accepts only camelCase tokens and keeps domain status and search", () => {
  const parsed = queueParams(
    { view: "approval", workStatus: "approvalRequired", search: "WXY 8210" },
    "workshop",
  );
  expect(parsed.filters).toMatchObject({
    status: "Approval",
    workStatus: "ApprovalRequired",
    search: "WXY 8210",
  });
  const query = new URLSearchParams(queueQuery(parsed.filters, 2));
  expect(query.get("view")).toBe("approval");
  expect(query.get("workStatus")).toBe("approvalRequired");
  expect(query.get("page")).toBe("2");
  expect(query.get("sort")).toBe("requestedAt");
  for (const view of ["Approval", "APPROVAL"]) {
    expect(queueParams({ view, workStatus: "ApprovalRequired" }, "workshop").filters).toMatchObject(
      {
        status: "Active",
        workStatus: "",
      },
    );
  }
  for (const task of ["MyWork", "Waiting", "Claimable"] as const) {
    const prefix = task[0].toLowerCase() + task.slice(1);
    const current = {
      [`${prefix}Search`]: "  XYZ  ",
      [`${prefix}Page`]: "2",
      [`${prefix}PageSize`]: "10",
    };
    expect(technicianQueueParams(current, task)).toBe("XYZ");
    expect(technicianQueuePaging(current, task)).toEqual({ page: 2, pageSize: 10 });
    expect(technicianQueueParams({ [`${task}.search`]: "ABC" }, task)).toBe("");
    expect(new URLSearchParams(technicianQueueQuery(task, "XYZ")).get("view")).toBe(prefix);
  }
});

test("workshop addresses put the record in the path and omit defaults", () => {
  const id = "11111111-1111-4111-8111-111111111111";
  const requestId = "22222222-2222-4222-8222-222222222222";
  const retained = `/dashboard/workshop/${id}/repairs/${requestId}/revisions/3`;
  expect(workshopLocation("/dashboard/workshop", "", false)?.href).toBe("/dashboard/workshop");
  expect(workshopLocation(`/dashboard/workshop/${id}`, "", false)?.href).toBe(
    `/dashboard/workshop/${id}`,
  );
  expect(workshopLocation(retained, "view=approval&page=2", false)).toMatchObject({
    id,
    section: "repairs",
    request: { requestId, revision: 3 },
    href: `${retained}?page=2&view=approval`,
  });
  expect(
    workshopLocation(`/dashboard/workshop/${id}/history`, "sort=requestedAt", false)?.href,
  ).toBe(`/dashboard/workshop/${id}/history?sort=requestedAt`);
  expect(
    workshopLocation(`/dashboard/workshop/${id}`, "inspection=other&section=history", false)?.href,
  ).toBe(`/dashboard/workshop/${id}`);
  for (const path of [
    `/dashboard/workshop/${id}/inspection`,
    `/dashboard/workshop/${id}/repairs/${requestId}`,
    `/dashboard/workshop/${id}/repairs/${requestId}/revisions/0`,
    `/dashboard/workshop/${id}/history/extra`,
    "/dashboard/workshop/not-an-id",
  ]) {
    expect(workshopLocation(path, "", false)).toBeNull();
  }
  const technician = workshopLocation(
    "/dashboard/workshop",
    "view=claimable&waitingPage=2&waitingSearch=ABC&myWorkPageSize=50",
    true,
  );
  expect(technician?.view).toBe("Claimable");
  expect(technician?.href).toBe(
    "/dashboard/workshop?view=claimable&myWorkPageSize=50&waitingSearch=ABC&waitingPage=2",
  );
  expect(workshopLocation("/dashboard/workshop", "view=myWork", true)?.href).toBe(
    "/dashboard/workshop",
  );
});

test("workshop checklist category is a query key only for a selected inspection", () => {
  const id = "11111111-1111-4111-8111-111111111111";
  const record = `/dashboard/workshop/${id}`;
  expect(workshopLocation(record, "category=exteriorPaint", false)).toMatchObject({
    category: "exteriorPaint",
    href: `${record}?category=exteriorPaint`,
  });
  expect(workshopLocation(record, "category=ExteriorPaint", false)?.href).toBe(record);
  expect(workshopLocation(record, "category=unknown", false)?.href).toBe(record);
  expect(workshopLocation(`${record}/history`, "category=exteriorPaint", false)?.href).toBe(
    `${record}/history`,
  );
  expect(workshopLocation("/dashboard/workshop", "category=exteriorPaint", true)?.href).toBe(
    "/dashboard/workshop",
  );
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

test("public inspection guide validates the catalog and applies supplied grading and warranty gates", () => {
  const policy = {
    gradeBMinimumRepairCost: 600,
    gradeCMinimumRepairCost: 3000,
    maximumRepairCost: 18000,
  };
  const guide = parseInspectionGuide({
    catalogVersion: "example-catalog",
    grading: policy,
    points: [
      { code: "FC-001", category: "FrameChassis", description: "Frame" },
      { code: "ET-001", category: "EngineTransmission", description: "Engine" },
      { code: "EP-001", category: "ExteriorPaint", description: "Exterior" },
      { code: "IE-001", category: "InteriorElectrical", description: "Interior" },
      { code: "UB-001", category: "UnderbodyBrakesSuspension", description: "Underbody" },
    ],
  });
  expect(guide).not.toBeNull();
  expect(inspectionCategories(guide!.points).every((category) => category.count === 1)).toBe(true);

  expect(evaluateInspectionOutcome(0, "Advisory", policy)?.grade).toBe("A");
  expect(evaluateInspectionOutcome(599.99, "Advisory", policy)?.grade).toBe("A");
  expect(evaluateInspectionOutcome(600, "Advisory", policy)?.grade).toBe("B");
  expect(evaluateInspectionOutcome(0, "MinorDefect", policy)?.grade).toBe("B");
  expect(evaluateInspectionOutcome(2999.99, "MinorDefect", policy)?.grade).toBe("B");
  expect(evaluateInspectionOutcome(3000, "Advisory", policy)?.grade).toBe("C");
  expect(evaluateInspectionOutcome(0, "MajorDefect", policy)?.grade).toBe("C");
  expect(evaluateInspectionOutcome(18000, "Advisory", policy)?.grade).toBe("C");
  expect(evaluateInspectionOutcome(18000.01, "Advisory", policy)).toMatchObject({
    grade: null,
    isRejected: true,
  });
  expect(evaluateInspectionOutcome(0, "CriticalDisqualifier", policy)).toMatchObject({
    grade: null,
    isRejected: true,
  });

  expect(isWarrantyPlanEligible("A", "A")).toBe(true);
  expect(isWarrantyPlanEligible("A", "B")).toBe(false);
  expect(isWarrantyPlanEligible("B", "B")).toBe(true);
  expect(isWarrantyPlanEligible("C", "C")).toBe(true);
  expect(isWarrantyPlanEligible("C", null)).toBe(false);
});
