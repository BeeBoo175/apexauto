import { isRecord } from "./contracts";

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
