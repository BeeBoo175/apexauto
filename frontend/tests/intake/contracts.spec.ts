import { expect, test } from "@playwright/test";
import { parseActor, actorDestination } from "@/lib/auth/actor";
import {
  defaultIntakeDateWindow,
  isSundayDate,
  validateIntakeDate,
} from "@/lib/intake/appointment-date";
import {
  isValidMalaysianPlate,
  normalizeMalaysianPlate,
  sanitizeRegistrationPlateInput,
  validateMalaysianPlate,
} from "@/lib/intake/registration-plate";
import { parseBooking, parseSlots, parseTaxonomy } from "@/lib/intake/parse";
import { bookingMatchesRequest } from "@/lib/intake/receipt";
import { csvCell, selectedCsv } from "@/lib/csv";
import { intakeQueueParams, staffParams, staffQuery, staffViewQuery } from "@/lib/staff/query";
import { intakeLocation, intakeLocationId } from "@/lib/staff/intake-location";
import { DEFAULT_PAGE_SIZE, PAGE_SIZES, paginationQuery } from "@/lib/pagination";
import { parsePage } from "@/lib/api";

import { appliedIntakeFilters, resetWorklistFilters } from "@/lib/staff/filter-summary";
import {
  addCalendarDays,
  calendarDate,
  calendarRange,
  calendarTime,
  addCalendarMonths,
} from "@/lib/calendar";

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

test("agent queues discard hidden table and calendar criteria and restore only queue context", () => {
  const input = {
    view: "history",
    page: "3",
    pageSize: "10",
    search: "VCA",
    handlerId: id,
    handler: "hidden handler",
    branchId: id,
    from: "2026-10-20",
    to: "2026-10-21",
    registration: "other plate",
    sort: "customerName",
    direction: "desc",
  };
  const { filters, page } = intakeQueueParams(input);
  expect(filters.queue).toBe("history");
  expect(filters.status).toBe("all");
  expect(Object.fromEntries(new URLSearchParams(staffViewQuery(filters, page)))).toEqual({
    view: "history",
    page: "3",
    pageSize: "10",
    search: "VCA",
  });
  expect(intakeQueueParams({ view: "unknown" }).filters.queue).toBe("myWork");
  for (const view of ["constructor", "toString", "__proto__"]) {
    expect(intakeQueueParams({ view }).filters.queue).toBe("myWork");
  }
  expect(intakeQueueParams({}).filters.queue).toBe("myWork");
  expect(staffViewQuery(intakeQueueParams({ view: "MyWork" }).filters, 1)).toBe("");
  expect(intakeLocation(intakeQueueParams({ view: "toClaim" }).filters, 2, id)).toBe(
    `/dashboard/intake/${id}?page=2&view=toClaim`,
  );
  expect(intakeLocationId("/dashboard/intake")).toBe("");
  expect(intakeLocationId(`/dashboard/intake/${id}`)).toBe(id);
  expect(intakeLocationId("/dashboard/intake/not-a-uuid")).toBeNull();
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
    branchId: id,
    search: " VCA %_! ",
    sort: "registrationPlate",
    direction: "desc",
    pageSize: "50",
    page: "3",
    view: "myWork",
    handlerId: id,
    handler: "aina.sofia",
  };
  const { filters, page } = staffParams(input);
  expect(page).toBe(3);
  const query = new URLSearchParams(staffQuery(filters, page));
  expect(Object.fromEntries(query)).toEqual({
    ...input,
    view: "myWork",
    search: "VCA %_!",
  });
  expect(staffParams({ view: "checkedIn" }).filters).toMatchObject({
    status: "checkedIn",
    queue: "",
  });
  expect(staffParams({ view: "toClaim" }).filters).toMatchObject({
    status: "all",
    queue: "toClaim",
  });
  expect(staffParams({ view: "noShow" }).filters.status).toBe("noShow");
  expect(staffParams({}).filters).toMatchObject({
    sort: "startsAt",
    direction: "asc",
    pageSize: 25,
    status: "all",
  });
  expect(staffViewQuery(staffParams({}).filters, 1)).toBe("");
  expect(new URLSearchParams(staffQuery(staffParams({}).filters, 1)).get("view")).toBe("all");
});

test("manager handler criteria remain visible, removable and reset without losing table order", () => {
  const { filters } = staffParams({
    handler: "aina.sofia",
    handlerId: id,
    sort: "handlerEmail",
    direction: "desc",
  });
  const applied = appliedIntakeFilters(filters, "Selected branch");
  expect(applied.map((filter) => filter.key)).toEqual(["handler", "handlerId"]);
  expect(applied[0].without.handler).toBe("");
  expect(applied[0].without.handlerId).toBe(id);
  expect(applied[1].without.handlerId).toBe("");
  expect(resetWorklistFilters(filters)).toMatchObject({
    handler: "",
    handlerId: "",
    sort: "handlerEmail",
    direction: "desc",
  });
});

test("actor destinations require a validated persisted role and branch shape", () => {
  expect(parseActor({ userId: id, role: "SalesAgent", branchId: null })).toBeNull();
  expect(parseActor({ userId: id, role: "Administrator", branchId: id })).toBeNull();
  expect(parseActor({ userId: id, role: "unknown", branchId: null })).toBeNull();
  expect(
    actorDestination({ userId: id, role: "Customer", branchId: null }, "/dashboard/intake"),
  ).toBe("/account");
  // Sales agents and managers land on Home, what needs them today.
  expect(actorDestination({ userId: id, role: "SalesAgent", branchId: id }, "/sell")).toBe(
    "/dashboard/home",
  );
  expect(actorDestination({ userId: id, role: "BranchManager", branchId: id })).toBe(
    "/dashboard/home",
  );
  expect(actorDestination({ userId: id, role: "Administrator", branchId: null })).toBe(
    "/dashboard/accounts",
  );
  expect(actorDestination({ userId: id, role: "WorkshopTechnician", branchId: id })).toBe(
    "/dashboard/workshop",
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

test("intake date bounds restrict picker window to 30 Malaysian days and reject Sundays", () => {
  // 2026-11-12T04:00:00Z is 2026-11-12 12:00 MYT (Thursday)
  const window = defaultIntakeDateWindow(new Date("2026-11-12T04:00:00Z"));
  expect(window).toEqual({ firstDate: "2026-11-12", lastDate: "2026-12-11" });
  expect(isSundayDate("2026-11-15")).toBe(true);
  expect(isSundayDate("2026-11-16")).toBe(false);
  expect(validateIntakeDate("2026-11-11", window)).toContain("30 days");
  expect(validateIntakeDate("2026-12-12", window)).toContain("30 days");
  expect(validateIntakeDate("2026-11-15", window)).toContain("Sundays");
  expect(validateIntakeDate("2026-11-14", window)).toBeNull();
  expect(validateIntakeDate("2026-11-16", window)).toBeNull();
});

test("Malaysian registration plate helper validates and normalizes JPJ formats", () => {
  expect(sanitizeRegistrationPlateInput("  w-1234  a! ")).toBe("W1234 A ");
  expect(normalizeMalaysianPlate("vca6242")).toBe("VCA 6242");
  expect(normalizeMalaysianPlate("  w  1234   a ")).toBe("W 1234 A");
  for (const valid of ["VCA 6242", "W 1234 A", "WYY8821", "KV 12 A", "A 1", "QAA 9999 C"]) {
    expect(isValidMalaysianPlate(valid)).toBe(true);
    expect(validateMalaysianPlate(valid, true)).toBeNull();
  }
  for (const invalid of ["123123", "W 0123", "UI 462078", "ABCD 123", "W 12345", "W 123 AB"]) {
    expect(isValidMalaysianPlate(invalid)).toBe(false);
    expect(validateMalaysianPlate(invalid, true)).toContain("Malaysian plate format");
  }
  expect(validateMalaysianPlate("   ", false)).toBeNull();
  expect(validateMalaysianPlate("   ", true)).toContain("Enter your Malaysian registration plate");
});
