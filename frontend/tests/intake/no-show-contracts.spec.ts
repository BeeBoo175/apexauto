import { expect, test } from "@playwright/test";
import {
  correctionMatches,
  parseAppointmentHandlers,
  parseCheckIn,
  parseDetail,
  parseWorklistItem,
} from "@/lib/staff/parse";
import { parseAppointment } from "@/lib/intake/parse";
import { ownershipMatches, parseOwnershipResult } from "@/lib/staff/ownership";
import { parseIntakeConflict } from "@/lib/intake/errors";
import { appointmentQueue } from "@/lib/staff/query";

const id = "c77708bf-1a31-42f5-9a69-0c8766697a52";

test("ownership receipts validate transitions and reconcile the original operation after handoff", () => {
  const nextId = "563681ca-021e-4982-bd3d-b27b6bc457c1";
  const receipt = {
    id,
    appointmentId: id,
    branchId: id,
    actorId: id,
    kind: "Claimed",
    handlerId: id,
    previousHandlerId: null,
    expectedVersion: 0,
    version: 1,
    reason: null,
    recordedAt: "2026-11-03T01:00:00Z",
  };
  const current = {
    ...receipt,
    id: nextId,
    kind: "Released",
    handlerId: null,
    previousHandlerId: id,
    expectedVersion: 1,
    version: 2,
  };
  const result = parseOwnershipResult({ receipt, current })!;
  expect(result).toEqual({ receipt, current });
  const request = { operationId: id, expectedVersion: 0, reason: null };
  expect(ownershipMatches(result, request, id, id, "claims")).toBe(true);
  expect(ownershipMatches(result, { ...request, operationId: nextId }, id, id, "claims")).toBe(
    false,
  );
  expect(ownershipMatches(result, { ...request, expectedVersion: 1 }, id, id, "claims")).toBe(
    false,
  );
  expect(ownershipMatches(result, request, nextId, id, "claims")).toBe(false);
  expect(ownershipMatches(result, request, id, nextId, "claims")).toBe(false);
  expect(ownershipMatches(result, request, id, id, "releases")).toBe(false);
  for (const invalid of [
    { ...receipt, version: 3 },
    { ...receipt, recordedAt: "2026-11-03T09:00:00+08:00" },
    { ...receipt, handlerId: nextId },
    { ...receipt, kind: "Released" },
    { ...receipt, kind: "Assigned", previousHandlerId: nextId },
  ]) {
    expect(parseOwnershipResult({ receipt: invalid, current })).toBeNull();
  }
  expect(
    parseOwnershipResult({ receipt, current: { ...current, appointmentId: nextId } }),
  ).toBeNull();
  expect(parseOwnershipResult({ receipt: current, current: receipt })).toBeNull();
  expect(parseOwnershipResult({ receipt, current: { ...receipt, id: nextId } })).toBeNull();
  for (const code of [
    "intake-ownership-conflict",
    "intake-ownership-unavailable",
    "intake-schedule-conflict",
  ]) {
    expect(parseIntakeConflict(code)?.code).toBe(code);
  }
  expect(parseIntakeConflict("unknown-ownership-code")).toBeUndefined();
});
const row = {
  id,
  branchId: id,
  branchName: "ApexAuto Glenmarie",
  customerName: "Tan Hui Min",
  registrationPlate: "VCA 6243",
  make: "Perodua",
  model: "Myvi",
  trim: "1.5 AV",
  manufactureYear: 2019,
  startsAt: "2026-10-20T01:00:00Z",
  endsAt: "2026-10-20T02:30:00Z",
  status: "NoShow",
  handlerId: null,
  handlerEmail: null,
  handlerEligible: false,
  ownershipVersion: 0,
};

test("no-show snapshots retain status and validate correction evidence", () => {
  expect(parseWorklistItem(row)?.status).toBe("NoShow");
  const detail = {
    ...row,
    make: "Perodua",
    model: "Myvi",
    trim: "1.5 AV",
    manufactureYear: 2019,
    mileageKilometres: 45000,
    noShowAt: "2026-10-20T16:00:00Z",
    attendanceCorrectedAt: null,
    correctionReason: null,
    correctedByEmail: null,
    customerMobile: "0123456789",
    customerEmail: "nur.aina@example.invalid",
    ownership: null,
  };
  expect(parseDetail(detail)?.status).toBe("NoShow");
  expect(appointmentQueue(parseDetail(detail)!)).toBe("History");
  expect(appointmentQueue(parseDetail({ ...detail, status: "Booked", noShowAt: null })!)).toBe(
    "ToClaim",
  );
  const ownership = {
    version: 2,
    handlerId: id,
    handlerEmail: "agent@example.invalid",
    handlerEligible: false,
    actorId: id,
    kind: "Assigned",
    recordedAt: row.startsAt,
    reason: "Branch handoff",
  };
  expect(parseDetail({ ...detail, ownership })?.ownership).toEqual(ownership);
  expect(
    appointmentQueue(parseDetail({ ...detail, status: "Booked", noShowAt: null, ownership })!),
  ).toBe("MyWork");
  expect(parseDetail({ ...detail, ownership: { ...ownership, version: 0 } })).toBeNull();
  expect(parseDetail({ ...detail, ownership: { ...ownership, kind: "Released" } })).toBeNull();
  const released = {
    ...ownership,
    kind: "Released",
    handlerId: null,
    handlerEmail: null,
  };
  expect(parseDetail({ ...detail, ownership: released })?.ownership).toEqual(released);
  expect(parseWorklistItem({ ...row, handlerId: id })).toBeNull();
  expect(parseAppointmentHandlers([{ id, email: ownership.handlerEmail }])).toEqual([
    { id, email: ownership.handlerEmail },
  ]);
  expect(parseAppointmentHandlers([{ id, email: "" }])).toBeNull();
  expect(
    parseAppointmentHandlers([
      { id, email: ownership.handlerEmail },
      { id, email: ownership.handlerEmail },
    ]),
  ).toBeNull();
  expect(parseDetail({ ...detail, noShowAt: "invalid" })).toBeNull();
  const history = {
    ...row,
    createdAt: "2026-10-19T01:00:00Z",
    cancelledAt: null,
    noShowAt: detail.noShowAt,
    checkedInAt: null,
  };
  expect(parseAppointment(history)?.noShowAt).toBe(detail.noShowAt);
  expect(parseAppointment({ ...history, noShowAt: "invalid" })).toBeNull();
});

test("a correction receipt must match the captured time, reason and physical evidence", () => {
  const evidence = {
    vin: "PL1BT65R84B123459",
    mileageKilometres: 92500,
    customerPresent: true,
    identityConfirmed: true,
    ownershipConfirmed: true,
    vehicleConfirmed: true,
    notes: null,
  };
  const receipt = {
    appointmentId: id,
    vehicleId: id,
    staffId: id,
    checkedInAt: row.startsAt,
    evidence,
    correction: { recordedAt: "2026-10-21T01:00:00Z", reason: "Branch log confirmed" },
  };
  const parsed = parseCheckIn(receipt)!;
  const request = {
    ...evidence,
    attendedAt: "2026-10-20T09:00:00+08:00",
    reason: " Branch log confirmed ",
  };
  expect(parsed).not.toBeNull();
  expect(correctionMatches(parsed, request)).toBe(true);
  expect(correctionMatches(parsed, { ...request, reason: "Different reason" })).toBe(false);
  expect(correctionMatches(parsed, { ...request, attendedAt: row.endsAt })).toBe(false);
  expect(
    parseCheckIn({ ...receipt, correction: { recordedAt: "invalid", reason: "Reason" } }),
  ).toBeNull();
});
