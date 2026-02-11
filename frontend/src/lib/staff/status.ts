export function appointmentStatusLabel(status: string) {
  if (status === "CheckedIn") {
    return "Checked in";
  }
  if (status === "NoShow") {
    return "No show";
  }
  return status;
}
