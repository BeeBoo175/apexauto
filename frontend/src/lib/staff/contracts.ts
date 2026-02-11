import type { HistoryFilters } from "@/lib/intake/list-query";

export const worklistStatuses = ["Booked", "CheckedIn", "Cancelled", "NoShow", "All"] as const;
export interface StaffFilters extends HistoryFilters {
  status: string;
  branchId: string;
  search: string;
  sort: string;
  direction: "asc" | "desc";
  pageSize: number;
}
export interface WorklistItem {
  id: string;
  branchId: string;
  branchName: string;
  customerName: string;
  registrationPlate: string;
  startsAt: string;
  endsAt: string;
  status: "Booked" | "CheckedIn" | "Cancelled" | "NoShow";
}
export interface AppointmentDetail extends WorklistItem {
  noShowAt: string | null;
  attendanceCorrectedAt: string | null;
  correctionReason: string | null;
  correctedBy: string | null;
  make: string;
  model: string;
  trim: string;
  manufactureYear: number;
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
