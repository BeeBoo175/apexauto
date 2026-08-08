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

export const showroomBranches: readonly ShowroomBranchInfo[] = [
  {
    id: "c77708bf-1a31-42f5-9a69-0c8766697a52",
    name: "ApexAuto Glenmarie",
    shortLabel: "Glenmarie",
    address: "12 Jalan Pengaturcara U1/51A",
    city: "Shah Alam",
    state: "Selangor",
    hours: SHOWROOM_OPERATING_HOURS,
    lat: 3.085,
    lng: 101.5829,
  },
  {
    id: "68f371ad-223a-43d6-b45b-b878eece79ae",
    name: "ApexAuto Petaling Jaya",
    shortLabel: "Petaling Jaya",
    address: "22 Jalan SS 2/55",
    city: "Petaling Jaya",
    state: "Selangor",
    hours: SHOWROOM_OPERATING_HOURS,
    lat: 3.1193,
    lng: 101.6183,
  },
  {
    id: "a50f99b1-8ff4-4ffb-b716-c82aa21fe117",
    name: "ApexAuto Bayan Lepas",
    shortLabel: "Bayan Lepas",
    address: "8 Jalan Mahsuri",
    city: "Bayan Lepas",
    state: "Pulau Pinang",
    hours: SHOWROOM_OPERATING_HOURS,
    lat: 5.3268,
    lng: 100.2887,
  },
];

export const dailyIntakeSlots: readonly IntakeSlotWindow[] = [
  { id: "09:00", windowLabel: "09:00 – 10:30", cutoffLabel: "Book by 07:00" },
  { id: "10:30", windowLabel: "10:30 – 12:00", cutoffLabel: "Book by 08:30" },
  { id: "12:00", windowLabel: "12:00 – 13:30", cutoffLabel: "Book by 10:00" },
  { id: "13:30", windowLabel: "13:30 – 15:00", cutoffLabel: "Book by 11:30" },
  { id: "15:00", windowLabel: "15:00 – 16:30", cutoffLabel: "Book by 13:00" },
  { id: "16:30", windowLabel: "16:30 – 18:00", cutoffLabel: "Book by 14:30" },
];
