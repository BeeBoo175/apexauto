import type { Branch } from "@/lib/branches";
import type { Page } from "@/lib/api";
import type { CustomerAccount } from "@/lib/auth/contracts";
export const PHOTO_DEADLINE_MS = 100_000;
export const PHOTO_MAX_BYTES = 10 * 1024 * 1024;
export const angles = ["Driver", "Passenger", "Front", "Rear"] as const;
export type PhotoAngle = (typeof angles)[number];

export interface VehicleInput {
  make: string;
  model: string;
  trim: string;
  manufactureYear: number;
  mileageKilometres: number;
}

export interface Taxonomy {
  currency: string;
  makes: {
    name: string;
    models: { name: string; trims: { name: string; baselinePrice: number }[] }[];
  }[];
}

export interface Quote {
  minValuation: number;
  maxValuation: number;
  currency: string;
  valuationYear: number;
}

export interface Slot {
  startsAt: string;
  endsAt: string;
}

export interface SlotOffer {
  branchId: string;
  date: string;
  dateWindow: { firstDate: string; lastDate: string };
  slots: Slot[];
}

export interface PhotoReceipt {
  id: string;
  angle: PhotoAngle;
  status: "Available" | "Pending" | "DeletionPending" | "Deleted";
  expiresAt: string;
  contentType: string;
  bytes: number;
  width: number;
  height: number;
}

export interface BookingReceipt {
  id: string;
  branchId: string;
  registrationPlate: string;
  slot: Slot;
  createdAt: string;
  vehicle: VehicleInput;
  quote: Quote;
  photos: { id: string; angle: PhotoAngle | number }[];
}

export interface Appointment {
  id: string;
  branchId: string;
  branchName: string;
  registrationPlate: string;
  startsAt: string;
  endsAt: string;
  createdAt: string;
  cancelledAt: string | null;
  noShowAt: string | null;
  checkedInAt: string | null;
}

export interface SellerSnapshot {
  taxonomy: Taxonomy | null;
  branches: Page<Branch> | null;
  account: CustomerAccount | null;
  access: number;
}
