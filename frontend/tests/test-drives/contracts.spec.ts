import { expect, test } from "@playwright/test";
import {
  matchesTestDriveBooking,
  parseTestDriveBooking,
  parseTestDriveCancellation,
  parseTestDriveHistory,
  parseTestDriveSlots,
} from "@/lib/test-drives/customer";
import { parseStaffTestDriveHistory, testDriveEventSummary } from "@/lib/test-drives/staff-history";
import {
  parseStaffTestDriveDetail,
  parseStaffTestDrivePage,
  parseTestDriveHandlers,
} from "@/lib/test-drives/staff";
import {
  parseTestDriveStaffReceipt,
  parseTestDriveStaffResponse,
  shapeTestDriveStaffCommand,
} from "@/lib/test-drives/staff-commands";
import {
  staffTestDriveParams,
  staffTestDriveQuery,
  staffTestDriveLocation,
  staffTestDriveUrl,
  testDriveDateError,
} from "@/lib/test-drives/staff-query";

const id = "47cf2705-95cc-48b6-b907-64bf18dd87da";
const customerId = "2f35e1b2-602b-43dc-8c5f-4085893b0ea8";
const vehicleId = "e26136c6-6e1b-46c2-9bb5-c28e2f01dd8c";
const receipt = {
  id,
  customerId,
  vehicleId,
  branchId: id,
  publicationId: vehicleId,
  startsAt: "2026-11-05T01:00:00Z",
  endsAt: "2026-11-05T01:30:00Z",
  createdAt: "2026-11-04T00:00:00Z",
};

test("booking receipts bind the captured operation, customer, vehicle, publication and booked instant", () => {
  const parsed = parseTestDriveBooking(receipt)!;
  const request = { operationId: id, publicationId: vehicleId, startsAt: receipt.startsAt };
  expect(matchesTestDriveBooking(parsed, request, vehicleId, customerId)).toBe(true);
  for (const change of [
    { id: customerId },
    { customerId: id },
    { vehicleId: id },
    { publicationId: id },
    { startsAt: "2026-11-05T02:00:00Z" },
  ]) {
    expect(matchesTestDriveBooking({ ...parsed, ...change }, request, vehicleId, customerId)).toBe(
      false,
    );
  }
  expect(parseTestDriveBooking({ ...receipt, endsAt: "2026-11-05T02:30:00Z" })).toBeNull();
  const offer = {
    vehicleId,
    publicationId: id,
    branchId: id,
    date: "2026-11-05",
    dateWindow: { firstDate: "2026-11-04", lastDate: "2026-12-03" },
    slots: [receipt],
  };
  expect(parseTestDriveSlots(offer)?.slots).toHaveLength(1);
  expect(parseTestDriveSlots({ ...offer, vehicleId: "" })).toBeNull();
  expect(parseTestDriveSlots({ ...offer, slots: [receipt, receipt] })).toBeNull();
  expect(parseTestDriveSlots({ ...offer, date: "2026-11-06" })).toBeNull();
});

test("customer history retains indefinite active work and rejects malformed lifecycle or private payload fields", () => {
  const item = {
    ...receipt,
    branchName: "Glenmarie",
    make: "Proton",
    model: "Saga",
    trim: "Premium",
    manufactureYear: 2022,
    status: "InProgress",
    startedAt: "2026-11-05T01:01:00Z",
    endedAt: null,
    cancelledAt: null,
    cancellationReason: null,
    outcomeReason: null,
    licenceNumber: "private",
  };
  const snapshot = {
    actor: { userId: customerId, role: "Customer", branchId: null },
    items: [item],
    total: 1,
    page: 1,
    pageSize: 5,
  };
  const parsed = parseTestDriveHistory(snapshot);
  expect(parsed?.items[0].status).toBe("InProgress");
  expect(JSON.stringify(parsed)).not.toContain("licenceNumber");
  expect(parseTestDriveHistory({ ...snapshot, pageSize: 25 })).toBeNull();
  expect(
    parseTestDriveHistory({ ...snapshot, actor: { ...snapshot.actor, role: "GeneralManager" } }),
  ).toBeNull();
  for (const change of [
    { status: "Completed" },
    { status: "Booked" },
    { status: "NoShow" },
    { cancelledAt: "2026-11-05T01:10:00Z" },
    { startedAt: null },
  ]) {
    expect(parseTestDriveHistory({ ...snapshot, items: [{ ...item, ...change }] })).toBeNull();
  }
  const cancellation = {
    id,
    bookingId: vehicleId,
    customerId,
    kind: "CustomerCancelled",
    staffId: null,
    reason: null,
    recordedAt: "2026-11-04T02:00:00Z",
  };
  expect(parseTestDriveCancellation(cancellation)?.customerId).toBe(customerId);
  expect(parseTestDriveCancellation({ ...cancellation, staffId: id })).toBeNull();
  expect(parseTestDriveCancellation({ ...cancellation, kind: "StaffCancelled" })).toBeNull();
});

test("staff snapshots preserve released ownership and bind handling evidence to the selected booking", () => {
  expect(staffTestDriveParams({ status: "toString" }, false).filters.status).toBe("all");
  expect(testDriveDateError({ from: "2026-02-30", to: "2026-03-03" })).not.toBe("");
  expect(testDriveDateError({ from: "2026-1-1", to: "2026-1-2" })).not.toBe("");
  expect(testDriveDateError({ from: "", to: "" })).toBe("");
  const hidden = {
    from: "2026-11-01",
    to: "2026-11-02",
    status: "Cancelled",
    branchId: id,
    handlerId: id,
    handler: "siti",
    registration: "BPL",
    vehicle: "Honda",
    customer: "Ahmad",
    sort: "handlerEmail",
    direction: "desc",
    queue: "ToClaim",
    search: "Saga",
  };
  const agentQuery = staffTestDriveParams(hidden, true);
  expect(agentQuery.filters).toMatchObject({
    from: "",
    to: "",
    status: "all",
    branchId: "",
    handlerId: "",
    handler: "",
    registration: "",
    vehicle: "",
    customer: "",
    sort: "",
    direction: "asc",
    queue: "toClaim",
    search: "Saga",
  });
  const managerQuery = staffTestDriveParams(hidden, false);
  expect(managerQuery.filters).toMatchObject({
    handler: "siti",
    registration: "BPL",
    vehicle: "Honda",
    customer: "Ahmad",
    handlerId: id,
    queue: "",
    sort: "handlerEmail",
    direction: "desc",
  });
  const sent = new URLSearchParams(staffTestDriveQuery(managerQuery.filters));
  expect(sent.get("handler")).toBe("siti");
  expect([sent.get("registration"), sent.get("vehicle"), sent.get("customer")]).toEqual([
    "BPL",
    "Honda",
    "Ahmad",
  ]);
  expect(
    new URLSearchParams(staffTestDriveQuery(staffTestDriveParams({}, false).filters)).has("sort"),
  ).toBe(false);
  const actor = { userId: customerId, role: "BranchManager", branchId: id };
  const booking = {
    ...receipt,
    branchName: "Glenmarie",
    registrationPlate: "WYY 8821",
    customerName: "Nur Aina Abdullah",
    make: "Proton",
    model: "Saga",
    trim: "Premium",
    manufactureYear: 2022,
    status: "Booked",
    assignmentId: vehicleId,
    ownershipVersion: 2,
    handlerId: null,
    handlerEmail: null,
    handlerName: null,
    handlerPictureId: null,
    handlerEligible: false,
  };
  const ownership = {
    id: vehicleId,
    bookingId: id,
    version: 2,
    handlerId: null,
    actorId: customerId,
    kind: "Released",
    recordedAt: "2026-11-04T02:00:00Z",
    reason: null,
  };
  const detail = {
    actor,
    booking,
    ownership,
    cancellation: null,
    start: null,
    outcome: null,
    createdAt: receipt.createdAt,
    customerMobile: "0123456789",
    customerEmail: "aina@example.invalid",
  };
  expect(parseStaffTestDriveDetail(detail)?.ownership?.version).toBe(2);
  expect(
    parseStaffTestDriveDetail({ ...detail, ownership: { ...ownership, bookingId: vehicleId } }),
  ).toBeNull();
  expect(
    parseStaffTestDriveDetail({ ...detail, ownership: { ...ownership, version: 1 } }),
  ).toBeNull();
  expect(
    parseStaffTestDriveDetail({ ...detail, booking: { ...booking, status: "InProgress" } }),
  ).toBeNull();
  const page = {
    actor,
    items: [booking],
    total: 1,
    page: 1,
    pageSize: 25,
    counts: {
      all: 1,
      booked: 1,
      inProgress: 0,
      completed: 0,
      notCompleted: 0,
      noShow: 0,
      cancelled: 0,
      toClaim: 1,
      myWork: 0,
      history: 0,
    },
  };
  expect(parseStaffTestDrivePage(page)?.items[0].assignmentId).toBe(vehicleId);
  expect(parseStaffTestDrivePage({ ...page, counts: null })).toBeNull();
  expect(
    parseStaffTestDrivePage({ ...page, actor: { ...actor, role: "WorkshopTechnician" } }),
  ).toBeNull();
  const handlers = { actor, items: [{ id: customerId, email: "siti@example.invalid" }] };
  expect(parseTestDriveHandlers(handlers)?.items).toHaveLength(1);
  expect(
    parseTestDriveHandlers({
      ...handlers,
      actor: { ...actor, role: "GeneralManager", branchId: null },
    }),
  ).toBeNull();
});

test("staff commands preserve exact ownership replay while rejecting other actors and incomplete attendance", () => {
  const actor = { userId: customerId, role: "SalesAgent" as const, branchId: id };
  const body = shapeTestDriveStaffCommand("claims", {
    operationId: vehicleId,
    expectedVersion: 0,
  })!;
  const ownership = {
    id: vehicleId,
    bookingId: id,
    version: 1,
    actorId: customerId,
    handlerId: customerId,
    kind: "Claimed",
    recordedAt: "2026-11-04T02:00:00Z",
    reason: null,
  };
  const current = {
    ...ownership,
    id,
    version: 2,
    handlerId: id,
    kind: "Assigned",
    reason: "Cover arrival",
  };
  expect(
    parseTestDriveStaffReceipt("claims", { receipt: ownership, current }, body, id, actor),
  ).not.toBeNull();
  for (const change of [{ id }, { actorId: id }, { bookingId: customerId }, { version: 2 }]) {
    expect(
      parseTestDriveStaffReceipt(
        "claims",
        { receipt: { ...ownership, ...change }, current },
        body,
        id,
        actor,
      ),
    ).toBeNull();
  }
  expect(shapeTestDriveStaffCommand("claims", { ...body, expectedVersion: -1 })).toBeNull();
  const startBody = {
    operationId: vehicleId,
    assignmentId: id,
    customerPresent: true,
    drivingLicenceChecked: true,
  };
  expect(shapeTestDriveStaffCommand("starts", startBody)).toEqual(startBody);
  expect(
    shapeTestDriveStaffCommand("starts", { ...startBody, drivingLicenceChecked: false }),
  ).toBeNull();
  const start = {
    id: vehicleId,
    bookingId: id,
    assignmentId: id,
    staffId: customerId,
    customerPresent: true,
    drivingLicenceChecked: true,
    startedAt: "2026-11-05T01:00:00Z",
  };
  expect(
    parseTestDriveStaffReceipt("starts", { start, outcome: null }, startBody, id, actor),
  ).not.toBeNull();
  expect(
    parseTestDriveStaffReceipt(
      "starts",
      { start: { ...start, assignmentId: customerId }, outcome: null },
      startBody,
      id,
      actor,
    ),
  ).toBeNull();
  expect(shapeTestDriveStaffCommand("outcomes", { ...startBody, kind: "NotCompleted" })).toBeNull();
  expect(
    shapeTestDriveStaffCommand("outcomes", {
      ...startBody,
      kind: "Completed",
      notes: "Returned safely",
    })?.notes,
  ).toBe("Returned safely");

  const cancellationBody = { operationId: vehicleId, reason: "Customer cannot attend" };
  const cancellation = {
    id: vehicleId,
    bookingId: id,
    customerId,
    staffId: customerId,
    kind: "StaffCancelled",
    reason: cancellationBody.reason,
    recordedAt: "2026-11-04T02:00:00Z",
  };
  const relayData = parseTestDriveStaffReceipt(
    "cancellations",
    cancellation,
    cancellationBody,
    id,
    actor,
  );
  expect(
    parseTestDriveStaffResponse("cancellations", relayData, cancellationBody, id, actor),
  ).toEqual(relayData);
  for (const change of [
    { id },
    { bookingId: customerId },
    { staffId: id },
    { reason: "Another reason" },
  ]) {
    expect(
      parseTestDriveStaffResponse(
        "cancellations",
        { cancellation: { ...cancellation, ...change } },
        cancellationBody,
        id,
        actor,
      ),
    ).toBeNull();
  }
});

test("a booking history keeps ordered, attributed steps and rejects unknown or partial ones", () => {
  const id = crypto.randomUUID();
  const actor = {
    userId: crypto.randomUUID(),
    role: "BranchManager",
    branchId: crypto.randomUUID(),
  };
  const event = {
    id,
    kind: "Assigned",
    recordedAt: "2026-11-05T02:00:00Z",
    actor: "Aina Sofia",
    actorId: actor.userId,
    handler: "Daniel Wong",
    handlerId: crypto.randomUUID(),
    reason: "Branch handoff",
    notes: null,
  };
  const page = { actor, bookingId: id, items: [event], totalCount: 1, page: 1, pageSize: 25 };
  expect(parseStaffTestDriveHistory(page)?.items[0].handler).toBe("Daniel Wong");
  expect(testDriveEventSummary(event)).toBe("Now with Daniel Wong.");
  expect(
    parseStaffTestDriveHistory({ ...page, items: [{ ...event, kind: "Promoted" }] }),
  ).toBeNull();
  expect(parseStaffTestDriveHistory({ ...page, items: [{ ...event, actor: " " }] })).toBeNull();
  expect(parseStaffTestDriveHistory({ ...page, items: [event, event] })).toBeNull();
  expect(parseStaffTestDriveHistory({ ...page, bookingId: "not-an-id" })).toBeNull();
});

test("staff test-drive locations normalize legacy tokens and preserve record sections and list context", () => {
  const manager = staffTestDriveParams({}, false);
  expect(staffTestDriveUrl(manager.filters, 1)).toBe("/dashboard/test-drives");
  expect(staffTestDriveUrl({ ...manager.filters, sort: "startsAt" }, 1)).toBe(
    "/dashboard/test-drives?sort=startsAt",
  );
  const agent = staffTestDriveParams({}, true);
  expect(staffTestDriveUrl(agent.filters, 1)).toBe("/dashboard/test-drives");
  const legacy = staffTestDriveLocation(
    "/dashboard/test-drives",
    `?booking=${id}&section=history&status=NotCompleted&page=2&pageSize=10`,
    false,
  )!;
  expect(legacy.selectedId).toBe(id);
  expect(legacy.filters.status).toBe("notCompleted");
  expect(legacy.href).toBe(
    `/dashboard/test-drives/${id}/history?page=2&pageSize=10&status=notCompleted`,
  );
  expect(
    staffTestDriveLocation(`/dashboard/test-drives/${vehicleId}`, `?booking=${id}`, false)
      ?.selectedId,
  ).toBe(vehicleId);
  expect(staffTestDriveLocation("/dashboard/test-drives/not-a-record", "", false)).toBeNull();
  expect(staffTestDriveParams({ queue: "ToClaim" }, true).filters.queue).toBe("toClaim");
  expect(staffTestDriveParams({ status: "constructor" }, false).filters.status).toBe("all");
});
