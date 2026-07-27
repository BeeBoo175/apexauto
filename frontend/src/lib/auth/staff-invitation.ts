import { isRecord } from "./contracts";

const conflictMessages: Record<string, string> = {
  "username-taken": "That username is taken. Choose another.",
  "email-taken": "An account already uses this email. Ask an administrator for help.",
  unavailable: "This invitation link is no longer available. Ask an administrator for a new one.",
};

export function staffActivationConflict(code: unknown) {
  return typeof code === "string" && Object.hasOwn(conflictMessages, code)
    ? conflictMessages[code]
    : conflictMessages.unavailable;
}

// What the public activation page may show before an employee signs up: no email, id or contact.
export interface StaffInvitationPreview {
  name: string;
  role: string;
  branchName: string | null;
}

export function parseStaffInvitation(value: unknown): StaffInvitationPreview | null {
  if (
    !isRecord(value) ||
    typeof value.name !== "string" ||
    !value.name.trim() ||
    typeof value.role !== "string" ||
    !value.role.trim() ||
    (value.branchName !== null && typeof value.branchName !== "string")
  ) {
    return null;
  }

  return { name: value.name, role: value.role, branchName: value.branchName };
}
