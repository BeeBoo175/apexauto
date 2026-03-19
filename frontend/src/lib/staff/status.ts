import type { WorklistItem } from "./contracts";

export function appointmentStatusLabel(status: string) {
  if (status === "CheckedIn") {
    return "Checked in";
  }
  if (status === "NoShow") {
    return "No show";
  }
  return status;
}

const statusTones = {
  Booked: "info",
  CheckedIn: "success",
  Cancelled: "neutral",
  NoShow: "neutral",
} as const;

export function appointmentStatusTone(status: WorklistItem["status"]) {
  return statusTones[status];
}
