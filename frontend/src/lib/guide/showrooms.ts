import type { Branch } from "@/lib/branches";
import {
  SHOWROOM_CLOSES_AT,
  SHOWROOM_CLOSING_MINUTES,
  SHOWROOM_OPENING_MINUTES,
  SHOWROOM_OPENS_AT,
} from "@/lib/guide/visit-terms";

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

export const SHOWROOM_OPERATING_HOURS = `Mon – Sat, ${SHOWROOM_OPENS_AT} – ${SHOWROOM_CLOSES_AT} MYT`;

const MALAYSIA_OFFSET_MS = 8 * 60 * 60 * 1000;

export interface ShowroomOpenStatus {
  open: boolean;
  label: string;
}

// Whether the showrooms are open at this moment and, if not, when they next open. Malaysia keeps UTC+8 all year,
// so the weekday and clock are read from a fixed offset rather than the visitor's own time zone.
export function showroomOpenStatus(now: Date): ShowroomOpenStatus {
  const malaysia = new Date(now.getTime() + MALAYSIA_OFFSET_MS);
  const weekday = malaysia.getUTCDay();
  const minutes = malaysia.getUTCHours() * 60 + malaysia.getUTCMinutes();
  const trading = weekday !== 0;

  if (trading && minutes >= SHOWROOM_OPENING_MINUTES && minutes < SHOWROOM_CLOSING_MINUTES) {
    return { open: true, label: `Open now, closes at ${SHOWROOM_CLOSES_AT}` };
  }
  if (trading && minutes < SHOWROOM_OPENING_MINUTES) {
    return { open: false, label: `Closed, opens today at ${SHOWROOM_OPENS_AT}` };
  }
  if (weekday >= 1 && weekday <= 5) {
    return { open: false, label: `Closed, opens tomorrow at ${SHOWROOM_OPENS_AT}` };
  }
  return { open: false, label: `Closed, opens Monday at ${SHOWROOM_OPENS_AT}` };
}

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
