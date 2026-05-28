import { expect, test } from "@playwright/test";
import {
  correctionMatches,
  parseAppointmentHandlers,
  parseCheckIn,
  parseDetail,
  parseWorklistItem,
} from "@/lib/staff/parse";
import { parseAppointment } from "@/lib/intake/parse";

const id = "c77708bf-1a31-42f5-9a69-0c8766697a52";
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
  expect(parseDetail({ ...detail, ownership: { ...ownership, version: 0 } })).toBeNull();
  expect(
    parseDetail({ ...detail, ownership: { ...ownership, kind: "Released" } }),
  ).toBeNull();
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
