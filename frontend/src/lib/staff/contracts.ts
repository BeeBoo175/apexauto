import type { CountedPage } from "@/lib/api";
import type { HistoryFilters } from "@/lib/intake/list-query";

export const worklistStatuses = ["Booked", "CheckedIn", "Cancelled", "NoShow", "All"] as const;
export const intakeQueues = ["ToClaim", "MyWork", "History"] as const;
export type IntakeQueue = (typeof intakeQueues)[number];
export interface StaffFilters extends HistoryFilters {
  status: string;
  branchId: string;
  search: string;
  sort: string;
  direction: "asc" | "desc";
  pageSize: number;
  registration?: string;
  vehicle?: string;
  customer?: string;
  queue?: IntakeQueue | "";
  handlerId?: string;
  handler?: string;
}
export interface AppointmentSummary {
  id: string;
  branchId: string;
  branchName: string;
  customerName: string;
  registrationPlate: string;
  make: string;
  model: string;
  trim: string;
  manufactureYear: number;
  startsAt: string;
  endsAt: string;
  status: "Booked" | "CheckedIn" | "Cancelled" | "NoShow";
}
export interface WorklistItem extends AppointmentSummary {
  handlerId: string | null;
  handlerEmail: string | null;
  handlerEligible: boolean;
  ownershipVersion: number;
}
export const intakeCountKeys = [
  "booked",
  "checkedIn",
  "cancelled",
  "noShow",
  "all",
  "toClaim",
  "myWork",
  "history",
] as const;
export type WorklistPage = CountedPage<WorklistItem, (typeof intakeCountKeys)[number]>;

export interface AppointmentOwnership {
  version: number;
  handlerId: string | null;
  handlerEmail: string | null;
  handlerEligible: boolean;
  actorId: string;
  kind: "Claimed" | "Assigned" | "Released";
  recordedAt: string;
  reason: string | null;
}
export interface AppointmentHandler {
  id: string;
  email: string;
}
export interface AppointmentDetail extends AppointmentSummary {
  ownership: AppointmentOwnership | null;
  noShowAt: string | null;
  attendanceCorrectedAt: string | null;
  correctionReason: string | null;
  correctedByEmail: string | null;
  customerMobile: string;
  customerEmail: string;
  mileageKilometres: number;
}
export interface CheckInDraft {
  attendedTime?: string;
  correctionReason?: string;
  vin: string;
  mileageKilometres: string;
  customerPresent: boolean;
  identityConfirmed: boolean;
  ownershipConfirmed: boolean;
  vehicleConfirmed: boolean;
  notes: string;
}
export const emptyCheckIn: CheckInDraft = {
  vin: "",
  mileageKilometres: "",
  customerPresent: false,
  identityConfirmed: false,
  ownershipConfirmed: false,
  vehicleConfirmed: false,
  notes: "",
};
export interface CheckInReceipt {
  appointmentId: string;
  vehicleId: string;
  staffId: string;
  checkedInAt: string;
  correction: { recordedAt: string; reason: string } | null;
  evidence: {
    vin: string;
    mileageKilometres: number;
    customerPresent: boolean;
    identityConfirmed: boolean;
    ownershipConfirmed: boolean;
    vehicleConfirmed: boolean;
    notes: string | null;
  };
}
