import type { ApiConflict } from "@/lib/api";

const messages: Record<string, string> = {
  "test-drive-operation-conflict":
    "This operation was used with different details. Reconcile the original request.",
  "test-drive-vehicle-unavailable": "This vehicle is unavailable for a test drive.",
  "customer-has-test-drive": "You already have an outstanding test drive.",
  "test-drive-slot-unavailable": "This slot is unavailable. Choose another time.",
  "test-drive-cancellation-conflict":
    "The drive or operation has changed. Refresh before continuing.",
  "test-drive-cancellation-too-late": "The cancellation deadline has passed.",
  "test-drive-ownership-conflict":
    "Ownership or operation details changed. Refresh before continuing.",
  "test-drive-ownership-unavailable": "The drive or handler is unavailable for assignment.",
  "test-drive-schedule-conflict":
    "The vehicle or handler has conflicting work. Wait or cancel the booked drive.",
  "test-drive-handling-conflict":
    "Ownership or operation details changed. Reconcile the original request.",
  "test-drive-handling-unavailable": "The drive or vehicle is unavailable for this action.",
};

export function parseTestDriveConflict(code: unknown): ApiConflict | undefined {
  if (typeof code !== "string" || !Object.hasOwn(messages, code)) {
    return undefined;
  }
  return { code, message: messages[code] };
}
