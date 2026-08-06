import { expect, test } from "@playwright/test";
import { calendarRange } from "@/lib/calendar";
import {
  parseWorkCalendar,
  parseUnscheduledWork,
  workCalendarParams,
  workCalendarQuery,
  hasWorkCalendarAccess,
} from "@/lib/staff/work-calendar";
import { workCalendarEvents, workItemHref } from "@/lib/staff/work-calendar-presentation";

const branchId = "c77708bf-1a31-42f5-9a69-0c8766697a52";
const id = "906a011f-8cba-4021-ae9c-f276b01df753";
const actor = { userId: id, role: "BranchManager", branchId };
const row = {
  id,
  kind: "Intake",
  branchId,
  branchName: "ApexAuto Glenmarie",
  registrationPlate: "VCA 9701",
  vehicle: "2020 Proton Saga Standard",
  status: "Booked",
  handlerId: null,
  handlerEmail: null,
  handlerName: null,
  handlerPictureId: null,
  startsAt: "2026-10-20T01:00:00Z",
  endsAt: "2026-10-20T02:30:00Z",
};
const scope = { actor, branchId, employee: "" };

test("shared calendar rejects partial, duplicate, oversized and wrong-range snapshots", () => {
  const snapshot = { ...scope, ...calendarRange("2026-10-20"), items: [row], total: 1 };
  expect(parseWorkCalendar(snapshot)?.items).toEqual([row]);
  expect(parseWorkCalendar({ ...snapshot, actor: null })).toBeNull();
  expect(parseWorkCalendar({ ...snapshot, total: 2 })).toBeNull();
  expect(parseWorkCalendar({ ...snapshot, to: "2026-11-09" })).toBeNull();
  expect(parseWorkCalendar({ ...snapshot, items: [row, row], total: 2 })).toBeNull();
  expect(parseWorkCalendar({ ...snapshot, branchId: id })).toBeNull();
  expect(
    parseWorkCalendar({ ...snapshot, items: [{ ...row, startsAt: "2026-11-09T00:00:00Z" }] }),
  ).toBeNull();
  const items = Array.from({ length: 1000 }, () => ({ ...row, id: crypto.randomUUID() }));
  expect(parseWorkCalendar({ ...snapshot, items, total: 1000 })?.items).toHaveLength(1000);
  expect(parseWorkCalendar({ ...snapshot, items: [...items, row], total: 1001 })).toBeNull();
  const shared = parseWorkCalendar({
    ...snapshot,
    items: [row, { ...row, kind: "TestDrive" }],
    total: 2,
  })!;
  expect(new Set(workCalendarEvents(shared).map((item) => item.id)).size).toBe(2);
  expect(workCalendarEvents(shared)[0].title).toBe("Intake · VCA 9701");
  expect(workCalendarEvents({ ...shared, branchId: null })[0].title).toBe(
    "Intake · VCA 9701 · ApexAuto Glenmarie",
  );
});

test("unscheduled work validates ownership and real editor identity without inventing dates", () => {
  const item = {
    ...row,
    kind: "Workshop",
    handlerId: id,
    handlerEmail: "aina@example.invalid",
    handlerName: "Aina Rahman",
    handlerPictureId: null,
    inspectionId: branchId,
  };
  const snapshot = {
    ...scope,
    items: [item],
    total: 1,
    page: 1,
    pageSize: 25,
    counts: { all: 1, workshop: 1, sale: 0 },
  };
  const work = parseUnscheduledWork(snapshot)!;
  expect(work.items[0]).not.toHaveProperty("startsAt");
  expect(workItemHref(work.items[0])).toBe(`/dashboard/workshop/${branchId}`);
  expect(
    parseUnscheduledWork({ ...snapshot, items: [{ ...item, inspectionId: null }] }),
  ).toBeNull();
  expect(parseUnscheduledWork({ ...snapshot, items: [{ ...item, handlerId: null }] })).toBeNull();
  expect(parseUnscheduledWork({ ...snapshot, pageSize: 5 })).toBeNull();
  expect(
    parseUnscheduledWork({ ...snapshot, counts: { all: 2, workshop: 1, sale: 0 } }),
  ).toBeNull();
  const filters = workCalendarParams({
    date: "2027-01-01",
    search: " aina ",
    assignment: "unassigned",
    view: "workshop",
    page: "2",
  });
  expect(Object.fromEntries(workCalendarQuery(filters))).toEqual({
    search: "aina",
    assignment: "unassigned",
    from: "2026-12-28",
    to: "2027-02-07",
  });
  expect(Object.fromEntries(workCalendarQuery(filters, true))).toEqual({
    search: "aina",
    view: "workshop",
    page: "2",
    pageSize: "25",
  });
});

test("only general managers accept an all-branch calendar and technicians have no calendar grant", () => {
  const snapshot = { ...scope, ...calendarRange("2026-10-20"), items: [row], total: 1 };
  const generalManager = { userId: id, role: "GeneralManager", branchId: null };
  expect(parseWorkCalendar({ ...snapshot, actor: generalManager, branchId: null })?.items).toEqual([
    row,
  ]);
  expect(parseWorkCalendar({ ...snapshot, branchId: null })).toBeNull();
  expect(
    parseWorkCalendar({ ...snapshot, actor: { ...actor, role: "WorkshopTechnician" } }),
  ).toBeNull();
  expect(hasWorkCalendarAccess("WorkshopTechnician")).toBe(false);
  expect(hasWorkCalendarAccess("Administrator")).toBe(false);
  expect(parseWorkCalendar({ ...snapshot, actor: generalManager, branchId: id })).toBeNull();
});

test("work calendar addresses omit defaults, keep camelCase tokens and scope by role", async () => {
  const { workCalendarLocation } = await import("../../src/lib/staff/work-calendar-location");
  const { calendarDate } = await import("../../src/lib/calendar");
  expect(workCalendarLocation("/dashboard/work-calendar", "", "BranchManager")?.href).toBe(
    "/dashboard/work-calendar",
  );
  expect(
    workCalendarLocation(
      "/dashboard/work-calendar",
      `date=${calendarDate()}&view=workshop&assignment=assigned&page=2&search=%20aina%20`,
      "BranchManager",
    )?.href,
  ).toBe("/dashboard/work-calendar?search=aina&view=workshop&assignment=assigned&page=2");
  expect(
    workCalendarLocation(
      "/dashboard/work-calendar",
      `branchId=${branchId}&view=workshop&assignment=assigned`,
      "SalesAgent",
    )?.href,
  ).toBe("/dashboard/work-calendar");
  expect(
    workCalendarLocation(
      "/dashboard/work-calendar",
      "view=Workshop&assignment=Assigned",
      "GeneralManager",
    )?.href,
  ).toBe("/dashboard/work-calendar");
});

test("work items open the role workspace on a path, never a selection query", () => {
  expect(workItemHref({ ...row, kind: "TestDrive" } as never)).toBe(`/dashboard/test-drives/${id}`);
  expect(workItemHref({ ...row, kind: "Intake" } as never)).toBe(`/dashboard/intake/${id}`);
});
