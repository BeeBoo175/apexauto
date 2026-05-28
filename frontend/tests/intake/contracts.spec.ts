import { expect, test } from "@playwright/test";
import { parseActor, actorDestination } from "@/lib/auth/actor";
import { parseBooking, parseSlots, parseTaxonomy } from "@/lib/intake/parse";
import { bookingMatchesRequest } from "@/lib/intake/receipt";
import { csvCell, selectedCsv } from "@/lib/csv";
import { intakeQueueParams, staffParams, staffQuery, staffViewQuery } from "@/lib/staff/query";
import { DEFAULT_PAGE_SIZE, PAGE_SIZES, paginationQuery } from "@/lib/pagination";
import { parsePage } from "@/lib/api";
import { intakeView, parseViewMemory, restoreViewHref } from "@/lib/staff/view-memory";
import {
  addCalendarDays,
  calendarDate,
  calendarRange,
  calendarTime,
  addCalendarMonths,
} from "@/lib/calendar";
import { calendarParams, calendarQuery, parseCalendar } from "@/lib/staff/calendar";
import { resizeColumnWidths } from "@/lib/column-resize";

const id = "c77708bf-1a31-42f5-9a69-0c8766697a52";

test("table divider growth borrows nearest-first and preserves the right edge", () => {
  const columns = [
    { id: "registration", minWidth: 144 },
    { id: "vehicle", minWidth: 176 },
    { id: "status", minWidth: 128 },
  ];
  const initial = { registration: 160, vehicle: 200, status: 180 };
  const grown = resizeColumnWidths(columns, initial, 0, 60);
  expect(grown).toEqual({ registration: 220, vehicle: 176, status: 144 });
  expect(Object.values(grown).reduce((sum, width) => sum + width, 0)).toBe(540);
  expect(initial).toEqual({ registration: 160, vehicle: 200, status: 180 });

  const clamped = resizeColumnWidths(columns, initial, 0, 1000);
  expect(clamped).toEqual({ registration: 236, vehicle: 176, status: 128 });
  expect(resizeColumnWidths(columns, clamped, 0, -1)).toEqual({
    registration: 235,
    vehicle: 177,
    status: 128,
  });
  expect(resizeColumnWidths(columns, initial, 1, -30)).toEqual({
    registration: 154,
    vehicle: 176,
    status: 210,
  });
});

test("list queries and snapshots share 10/25/50 sizes and the default 25", () => {
  expect(DEFAULT_PAGE_SIZE).toBe(25);
  expect(Object.fromEntries(paginationQuery())).toEqual({ page: "1", pageSize: "25" });
  for (const size of PAGE_SIZES) {
    expect(staffParams({ pageSize: String(size) }).filters.pageSize).toBe(size);
    expect(parsePage({ items: [], total: 0, page: 1, pageSize: size }, () => null)).not.toBeNull();
  }
  for (const size of [0, 1, 20, 51]) {
    expect(staffParams({ pageSize: String(size) }).filters.pageSize).toBe(25);
    expect(parsePage({ items: [], total: 0, page: 1, pageSize: size }, () => null)).toBeNull();
  }
});

test("view memory ignores corrupt, oversized and other-actor state and bounds restored settings", () => {
  const saved = JSON.stringify({ actor: "staff:branch", list: "page=3", calendar: 123 });
  expect(parseViewMemory(saved, "staff:branch")).toEqual({
    list: "page=3",
    calendar: undefined,
    queue: undefined,
  });
  expect(parseViewMemory(saved, "staff:other-branch")).toEqual({});
  expect(parseViewMemory("{broken", "staff:branch")).toEqual({});
  expect(parseViewMemory(" ".repeat(16385), "staff:branch")).toEqual({});
  const restored = restoreViewHref(
    "list",
    "/dashboard/intake?view=list",
    "page=-1&pageSize=500&sort=evil&search=%00",
  );
  expect(restored).toBe("/dashboard/intake?view=list");
});

test("agent queues discard hidden table and calendar criteria and restore only queue context", () => {
  const input = {
    view: "queue",
    queue: "History",
    page: "3",
    pageSize: "10",
    search: "VCA",
    status: "Booked",
    handlerId: id,
    branchId: id,
    from: "2026-10-20",
    to: "2026-10-21",
    registration: "other plate",
    sort: "customerName",
    direction: "desc",
  };
  const { filters, page } = intakeQueueParams(input);
  expect(Object.fromEntries(new URLSearchParams(staffViewQuery(filters, page)))).toEqual({
    view: "queue",
    queue: "History",
    page: "3",
    pageSize: "10",
    search: "VCA",
  });
  expect(intakeQueueParams({ queue: "unknown" }).filters.queue).toBe("ToClaim");
  expect(intakeView(undefined, true)).toBe("queue");
  expect(intakeView("list", true)).toBe("queue");
  expect(intakeView(undefined, false)).toBe("list");
  expect(intakeView("queue", false)).toBe("list");
  expect(intakeView("calendar", true)).toBe("calendar");
  expect(
    restoreViewHref("queue", "/dashboard/intake?view=queue", new URLSearchParams(input).toString()),
  ).toBe(`/dashboard/intake?${staffViewQuery(filters, page)}`);
});

test("appointment dates use Malaysian midnight and retain year/leap boundaries", () => {
  expect(calendarDate(new Date("2026-10-19T15:59:59Z"))).toBe("2026-10-19");
  expect(calendarDate(new Date("2026-10-19T16:00:00Z"))).toBe("2026-10-20");
  expect(calendarRange("2027-01-31")).toEqual({ from: "2026-12-28", to: "2027-02-07" });
  expect(calendarRange("2028-02-29")).toEqual({ from: "2028-01-31", to: "2028-03-12" });
  expect(calendarRange("2026-02-01")).toEqual({ from: "2026-01-26", to: "2026-03-08" });
  expect(addCalendarDays("2028-02-28", 1)).toBe("2028-02-29");
  expect(addCalendarMonths("2027-01-31", -1)).toBe("2026-12-01");
  expect(addCalendarMonths("2028-01-31", 1)).toBe("2028-02-01");
  expect(calendarTime("2026-10-20T01:00:00Z", "Asia/Kuala_Lumpur")).toContain("09:00");
  const filters = calendarParams({ date: "2027-01-01", branchId: id });
  expect(Object.fromEntries(new URLSearchParams(calendarQuery(filters)))).toEqual({
    from: "2026-12-28",
    to: "2027-02-07",
    status: "All",
    branchId: id,
  });
  expect(calendarParams({}).status).toBe("All");
  expect(calendarParams({}).branchId).toBe("");
});

test("calendar snapshots refuse truncated, duplicate, out-of-range and oversized results", () => {
  const row = {
    id,
    branchId: id,
    branchName: "ApexAuto Glenmarie",
    customerName: "Nur Aina Abdullah",
    registrationPlate: "VCA 9701",
    make: "Proton",
    model: "Saga",
    trim: "Standard",
    manufactureYear: 2020,
    startsAt: "2026-10-20T01:00:00Z",
    endsAt: "2026-10-20T02:30:00Z",
    status: "Booked",
    handlerId: null,
    handlerEmail: null,
    handlerEligible: false,
    ownershipVersion: 0,
  };
  const range = calendarRange("2026-10-20");
  const snapshot = { ...range, items: [row], total: 1 };
  expect(parseCalendar(snapshot, range)?.items).toEqual([row]);
  expect(parseCalendar({ ...snapshot, total: 2 })).toBeNull();
  expect(parseCalendar({ ...snapshot, to: "2026-10-20" }, range)).toBeNull();
  expect(parseCalendar({ ...snapshot, to: addCalendarDays(range.to, 1) })).toBeNull();
  expect(parseCalendar({ ...snapshot, from: snapshot.to })).toBeNull();
  expect(parseCalendar({ ...snapshot, items: [row, row], total: 2 })).toBeNull();
  expect(
    parseCalendar({ ...snapshot, items: [{ ...row, startsAt: "2026-11-09T00:00:00Z" }] }),
  ).toBeNull();
  const rows = Array.from({ length: 1000 }, () => ({ ...row, id: crypto.randomUUID() }));
  expect(parseCalendar({ ...snapshot, items: rows, total: 1000 })?.items).toHaveLength(1000);
  expect(parseCalendar({ ...snapshot, items: [...rows, row], total: 1001 })).toBeNull();
  expect(parseCalendar({ ...range, items: [], total: 0 })?.total).toBe(0);
});
test("CSV quotes multiline cells and neutralizes formula and control prefixes", () => {
  for (const value of [
    "=SUM(1,2)",
    " +123",
    "\uFEFF-1",
    " \u0001@name",
    "\tplain",
    "\u0085plain",
  ]) {
    expect(csvCell(value)).toBe(`"'${value}"`);
  }
  expect(csvCell('Nur "Aina", Abdullah\nSelangor')).toBe('"Nur ""Aina"", Abdullah\nSelangor"');
  expect(csvCell(" ")).toBe('" "');
  const csv = selectedCsv(
    [{ value: "VCA 9701" }],
    [
      {
        label: "Registration",
        value: (row) => row.value,
      },
    ],
  );
  expect(csv).toBe('\uFEFF"Registration"\r\n"VCA 9701"\r\n');
});

test("staff queries preserve applied search, sorting, branch and page size through navigation", () => {
  const input = {
    from: "2026-10-20",
    to: "2026-10-29",
    status: "All",
    branchId: id,
    search: " VCA %_! ",
    sort: "registrationPlate",
    direction: "desc",
    pageSize: "50",
    page: "3",
    queue: "MyWork",
    handlerId: id,
  };
  const { filters, page } = staffParams(input);
  expect(page).toBe(3);
  const query = new URLSearchParams(staffQuery(filters, page));
  expect(Object.fromEntries(query)).toEqual({ ...input, search: "VCA %_!" });
  expect(staffParams({}).filters).toMatchObject({
    sort: "startsAt",
    direction: "asc",
    pageSize: 25,
    status: "All",
  });
});

test("actor destinations require a validated persisted role and branch shape", () => {
  expect(parseActor({ userId: id, role: "SalesAgent", branchId: null })).toBeNull();
  expect(parseActor({ userId: id, role: "Administrator", branchId: id })).toBeNull();
  expect(parseActor({ userId: id, role: "unknown", branchId: null })).toBeNull();
  expect(
    actorDestination({ userId: id, role: "Customer", branchId: null }, "/dashboard/intake"),
  ).toBe("/account");
  expect(actorDestination({ userId: id, role: "SalesAgent", branchId: id }, "/sell")).toBe(
    "/dashboard/intake",
  );
  expect(actorDestination({ userId: id, role: "Administrator", branchId: null })).toBe(
    "/dashboard/settings",
  );
});

test("external intake snapshots reject partial taxonomy and impossible calendar dates", () => {
  expect(parseTaxonomy({ currency: "MYR", makes: [{ name: "Proton", models: [{}] }] })).toBeNull();
  expect(
    parseSlots({
      branchId: id,
      date: "2026-02-31",
      dateWindow: { firstDate: "2026-02-01", lastDate: "2026-03-01" },
      slots: [],
    }),
  ).toBeNull();
});

test("booking receipts bind the exact operation and four distinct photo angles", () => {
  const vehicle = {
    make: "Proton",
    model: "Saga",
    trim: "Standard",
    manufactureYear: 2020,
    mileageKilometres: 90000,
  };
  const quote = { minValuation: 10000, maxValuation: 12000, currency: "MYR", valuationYear: 2026 };
  const photos = ["Driver", "Passenger", "Front", "Rear"].map((angle) => ({
    id: crypto.randomUUID(),
    angle,
  }));
  const receipt = {
    id,
    branchId: id,
    registrationPlate: "VCA 9701",
    slot: { startsAt: "2026-10-20T01:00:00Z", endsAt: "2026-10-20T02:30:00Z" },
    createdAt: "2026-10-19T01:00:00Z",
    vehicle,
    quote,
    photos,
  };
  const request = {
    id,
    branchId: id,
    registrationPlate: "  vca 9701  ",
    startsAt: receipt.slot.startsAt,
    vehicle,
    acceptedQuote: { ...quote, valuationYear: 2025 },
    photoIds: photos.map((photo) => photo.id),
  };
  expect(bookingMatchesRequest(receipt, request)).toBe(true);
  expect(bookingMatchesRequest(receipt, { ...request, id: crypto.randomUUID() })).toBe(false);
  expect(bookingMatchesRequest(receipt, { ...request, branchId: crypto.randomUUID() })).toBe(false);
  expect(bookingMatchesRequest(receipt, { ...request, registrationPlate: "VCA9701" })).toBe(false);
  expect(bookingMatchesRequest(receipt, { ...request, startsAt: receipt.slot.endsAt })).toBe(false);
  expect(
    bookingMatchesRequest(receipt, { ...request, vehicle: { ...vehicle, mileageKilometres: 1 } }),
  ).toBe(false);
  for (const acceptedQuote of [
    { ...quote, minValuation: 9999 },
    { ...quote, maxValuation: 12001 },
    { ...quote, currency: "USD" },
  ]) {
    expect(bookingMatchesRequest(receipt, { ...request, acceptedQuote })).toBe(false);
  }
  expect(
    bookingMatchesRequest(receipt, {
      ...request,
      photoIds: [...request.photoIds, crypto.randomUUID()],
    }),
  ).toBe(false);
  expect(
    bookingMatchesRequest(receipt, { ...request, photoIds: photos.map(() => crypto.randomUUID()) }),
  ).toBe(false);
  expect(
    parseBooking({ ...receipt, photos: photos.map((photo) => ({ ...photo, angle: "Driver" })) }),
  ).toBeNull();
});
