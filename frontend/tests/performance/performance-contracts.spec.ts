import { expect, test } from "@playwright/test";
import {
  parseBranches,
  parseHeadline,
  parsePipeline,
  parseStageWaiting,
  parseStockFlow,
  parseWorkload,
} from "@/lib/performance/performance";
import { parseRoomUrl, roomUrlValues, workloadQuery } from "@/lib/performance/room";
import { parseSalesTeam, parseWorkshopTeam } from "@/lib/performance/team";

const id = "9b2f6f0e-4c0e-4f6e-8b0e-0a8f5d2c1e11";
const otherId = "1c7a2d34-5b6e-4f80-9a1b-2c3d4e5f6a7b";
const actor = { userId: id, role: "BranchManager", branchId: id, emailVerified: true };
const totals = {
  vehiclesDelivered: 2,
  deliveredValue: 120000.5,
  listedCount: 4,
  checkInToListedDays: null,
  customerNoShows: 1,
};
const week = { weekStart: "2026-11-02", checkIns: 3, deliveries: 1, deliveredValue: 55000 };
const person = {
  id,
  name: "Aina Sofia",
  role: "SalesAgent",
  branchId: id,
  branchName: "Glenmarie",
  pictureId: null,
  under2Days: 1,
  twoToSevenDays: 2,
  overSevenDays: 0,
  total: 3,
};
const workload = {
  actor,
  people: [person],
  total: 1,
  salesCount: 1,
  workshopCount: 0,
  stretchedCount: 0,
  page: 1,
  pageSize: 25,
};
const teamPerson = {
  personId: id,
  name: "Aina Sofia",
  role: "SalesAgent",
  branchId: id,
  branchName: "Glenmarie",
  pictureId: null,
  status: "Active",
};
const salesRow = {
  ...teamPerson,
  checkIns: 4,
  noShowCorrections: 0,
  drivesCompleted: 2,
  drivesNotCompleted: 1,
  salesSettled: 1,
  salesDelivered: 1,
  assignmentToDeliveryDays: 2.5,
  intakeHandled: null,
  testDrivesHeld: null,
  salesHandled: null,
};
const workshopRow = {
  ...teamPerson,
  role: "WorkshopTechnician",
  inspectionsSigned: 3,
  reinspections: 0,
  repairsCompleted: 1,
  assignmentToSignOffDays: null,
  assignmentToReinspectionDays: null,
  openDrafts: 1,
  openRepairs: null,
};
const point = { personId: id, branchId: id, name: "Aina Sofia", volume: 4, days: null };
const teamPage = (row: object) => ({
  actor,
  items: [row],
  total: 1,
  page: 1,
  pageSize: 25,
  points: [point],
});

test("headline totals need whole counts and a real value, and keep the actor", () => {
  expect(parseHeadline({ actor, current: totals, previous: totals })?.current.deliveredValue).toBe(
    120000.5,
  );
  expect(
    parseHeadline({ actor, current: totals, previous: { ...totals, listedCount: -1 } }),
  ).toBeNull();
  expect(
    parseHeadline({ actor, current: totals, previous: { ...totals, deliveredValue: "1" } }),
  ).toBeNull();
  expect(parseHeadline({ actor: null, current: totals, previous: totals })).toBeNull();
});

test("pipeline groups and stages accept only the named groups and stages", () => {
  const group = { group: "SettledUndelivered", count: 2, agreedTotal: 90000 };
  expect(parsePipeline({ actor, groups: [group] })?.groups).toHaveLength(1);
  expect(parsePipeline({ actor, groups: [{ ...group, group: "Invented" }] })).toBeNull();
  const stage = { stage: "SaleInProgress", inStageNow: 1, completedCount: 2, averageDays: null };
  expect(parseStageWaiting({ actor, stages: [stage] })?.stages).toHaveLength(1);
  expect(parseStageWaiting({ actor, stages: [{ ...stage, stage: "Invented" }] })).toBeNull();
  expect(parseStageWaiting({ actor, stages: [{ ...stage, averageDays: "2" }] })).toBeNull();
});

test("stock flow weeks need a date, whole counts and a delivered value that is not negative", () => {
  expect(parseStockFlow({ actor, weeks: [week] })?.weeks).toHaveLength(1);
  expect(parseStockFlow({ actor, weeks: [{ ...week, weekStart: "2 Nov 2026" }] })).toBeNull();
  expect(parseStockFlow({ actor, weeks: [{ ...week, deliveredValue: -1 }] })).toBeNull();
  const withoutValue: Partial<typeof week> = { ...week };
  delete withoutValue.deliveredValue;
  expect(parseStockFlow({ actor, weeks: [withoutValue] })).toBeNull();
});

test("workload pages keep the whole-scope counts and reject malformed people", () => {
  expect(parseWorkload(workload)?.stretchedCount).toBe(0);
  expect(parseWorkload({ ...workload, people: [{ ...person, id: "not-a-uuid" }] })).toBeNull();
  expect(parseWorkload({ ...workload, people: [{ ...person, total: 1.5 }] })).toBeNull();
  expect(parseWorkload({ ...workload, salesCount: undefined })).toBeNull();
  expect(parseWorkload({ ...workload, pageSize: -1 })).toBeNull();
});

test("branch comparison rows carry both periods and their weeks", () => {
  const branch = {
    branchId: id,
    branchName: "Glenmarie",
    current: totals,
    previous: totals,
    weeks: [week],
  };
  expect(parseBranches({ actor, branches: [branch] })?.branches).toHaveLength(1);
  expect(parseBranches({ actor, branches: [{ ...branch, previous: null }] })).toBeNull();
  expect(
    parseBranches({ actor, branches: [{ ...branch, weeks: [{ ...week, checkIns: -1 }] }] }),
  ).toBeNull();
});

test("team pages accept optional measures as null and reject duplicate people or unknown states", () => {
  expect(parseSalesTeam(teamPage(salesRow))?.items).toHaveLength(1);
  expect(parseWorkshopTeam(teamPage(workshopRow))?.points).toHaveLength(1);
  expect(
    parseSalesTeam({ ...teamPage(salesRow), items: [salesRow, salesRow], total: 2 }),
  ).toBeNull();
  expect(parseSalesTeam(teamPage({ ...salesRow, status: "Retired" }))).toBeNull();
  expect(parseSalesTeam(teamPage({ ...salesRow, assignmentToDeliveryDays: -1 }))).toBeNull();
  expect(
    parseWorkshopTeam({ ...teamPage(workshopRow), points: [{ ...point, volume: -1 }] }),
  ).toBeNull();
  const sameIdOtherBranch = { ...salesRow, branchId: otherId };
  expect(
    parseSalesTeam({ ...teamPage(salesRow), items: [salesRow, sameIdOtherBranch], total: 2 }),
  ).not.toBeNull();
});

test("Who has room reads only known address values and leaves defaults off the address", () => {
  expect(parseRoomUrl({})).toEqual({ team: "sales", search: "", page: 1, pageSize: 25 });
  expect(parseRoomUrl({ view: "workshop", search: "  Aina ", page: "3", pageSize: "50" })).toEqual({
    team: "workshop",
    search: "Aina",
    page: 3,
    pageSize: 50,
  });
  expect(parseRoomUrl({ view: "everyone", page: "-2", pageSize: "7" })).toEqual({
    team: "sales",
    search: "",
    page: 1,
    pageSize: 25,
  });
  expect(roomUrlValues({ team: "sales", search: "", page: 1, pageSize: 25 })).toEqual({
    view: "",
    search: "",
    page: "",
    pageSize: "",
  });
  const query = new URLSearchParams(
    workloadQuery({ team: "workshop", search: " Aina ", page: 2, pageSize: 10 }),
  );
  expect(Object.fromEntries(query)).toEqual({
    view: "workshop",
    search: "Aina",
    page: "2",
    pageSize: "10",
  });
});
