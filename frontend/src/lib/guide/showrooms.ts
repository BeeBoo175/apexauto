import type { Branch } from "@/lib/branches";

export type BranchRegionState = "Selangor" | "Pulau Pinang";

export interface ShowroomBranchInfo {
  id: string;
  name: string;
  shortLabel: string;
  address: string;
  city: string;
  state: BranchRegionState;
  hours: string;
  lat: number;
  lng: number;
}

export interface IntakeSlotWindow {
  id: string;
  windowLabel: string;
  cutoffLabel: string;
}

export const SHOWROOM_OPERATING_HOURS = "Mon – Sat, 09:00 – 18:00 MYT";

// Presentation details the branch record does not hold (D-07), keyed by the branch's stored short code so a
// database rebuild with new identities still finds them.
const showroomDetails: Record<string, Omit<ShowroomBranchInfo, "id" | "name">> = {
  GLN: {
    shortLabel: "Glenmarie",
    address: "12 Jalan Pengaturcara U1/51A",
    city: "Shah Alam",
    state: "Selangor",
    hours: SHOWROOM_OPERATING_HOURS,
    lat: 3.085,
    lng: 101.5829,
  },
  PJ: {
    shortLabel: "Petaling Jaya",
    address: "22 Jalan SS 2/55",
    city: "Petaling Jaya",
    state: "Selangor",
    hours: SHOWROOM_OPERATING_HOURS,
    lat: 3.1193,
    lng: 101.6183,
  },
  BL: {
    shortLabel: "Bayan Lepas",
    address: "8 Jalan Mahsuri",
    city: "Bayan Lepas",
    state: "Pulau Pinang",
    hours: SHOWROOM_OPERATING_HOURS,
    lat: 5.3268,
    lng: 100.2887,
  },
};

// The active branches the API lists, in its order, with their map details; a branch without details has no map
// position and is left out of the directory.
export function resolveShowrooms(branches: readonly Branch[]): ShowroomBranchInfo[] {
  return branches.flatMap((branch) => {
    const details = showroomDetails[branch.shortCode];
    return details ? [{ id: branch.id, name: branch.name, ...details }] : [];
  });
}
export const dailyIntakeSlots: readonly IntakeSlotWindow[] = [
  { id: "09:00", windowLabel: "09:00 – 10:30", cutoffLabel: "Book by 07:00" },
  { id: "10:30", windowLabel: "10:30 – 12:00", cutoffLabel: "Book by 08:30" },
  { id: "12:00", windowLabel: "12:00 – 13:30", cutoffLabel: "Book by 10:00" },
  { id: "13:30", windowLabel: "13:30 – 15:00", cutoffLabel: "Book by 11:30" },
  { id: "15:00", windowLabel: "15:00 – 16:30", cutoffLabel: "Book by 13:00" },
  { id: "16:30", windowLabel: "16:30 – 18:00", cutoffLabel: "Book by 14:30" },
];
