import type { WorklistItem } from "./contracts";

export function appointmentStatusLabel(status: string) {
  if (status === "CheckedIn" || status === "checkedIn") {
    return "Checked in";
  }
  if (status === "NoShow" || status === "noShow") {
    return "No show";
  }
  if (status === "booked") {
    return "Booked";
  }
  if (status === "cancelled") {
    return "Cancelled";
  }
  if (status === "all") {
    return "All";
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
