import type { ApiConflict } from "@/lib/api";

const intakeConflictMessages = {
  "intake-ownership-conflict":
    "Ownership changed or this operation was already used. Refresh the appointment to review its current handler.",
  "intake-ownership-unavailable":
    "This appointment or handler is unavailable. Refresh the appointment before continuing.",
  "intake-schedule-conflict":
    "The handler already has work during this appointment. Choose another handler or release the overlapping work.",
  "check-in-content-conflict":
    "This appointment was checked in with different details. Refresh to review the recorded check-in.",
  "check-in-unavailable":
    "This appointment or its evidence is unavailable for check-in. Refresh the appointment before continuing.",
  "vehicle-identity-conflict":
    "The VIN or registration already identifies a vehicle. Review the vehicle identity before continuing.",
};

export function parseIntakeConflict(value: unknown): ApiConflict | undefined {
  if (typeof value !== "string" || !Object.hasOwn(intakeConflictMessages, value)) {
    return undefined;
  }
  const code = value as keyof typeof intakeConflictMessages;
  return { code, message: intakeConflictMessages[code] };
}
