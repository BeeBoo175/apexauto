import { isRecord, isUuid } from "@/lib/auth/contracts";
import { staffAccountRoles } from "./staff-accounts";

// Roles tied to one branch; General managers and Administrators have none.
const branchRoles = ["SalesAgent", "WorkshopTechnician", "BranchManager"] as const;

export const isBranchRole = (role: string) => branchRoles.some((item) => item === role);

export interface StaffInvitationDraft {
  id: string;
  name: string;
  email: string;
  role: string;
  branchId: string;
  mobile: string;
}

export const emptyInvitation = (): StaffInvitationDraft => ({
  id: crypto.randomUUID(),
  name: "",
  email: "",
  role: "SalesAgent",
  branchId: "",
  mobile: "",
});

const text = (value: unknown, maximum: number): value is string =>
  typeof value === "string" && value.trim().length > 0 && value.trim().length <= maximum;

export function shapeInvitation(body: Record<string, unknown>) {
  const role = body.role;
  if (
    !isUuid(body.id) ||
    !text(body.name, 200) ||
    !text(body.email, 254) ||
    !text(body.mobile, 30) ||
    typeof role !== "string" ||
    !staffAccountRoles.some((item) => item === role) ||
    (isBranchRole(role) ? !isUuid(body.branchId) : body.branchId !== null && body.branchId !== "")
  ) {
    return null;
  }
  return {
    id: body.id,
    name: body.name.trim(),
    email: body.email.trim(),
    role,
    branchId: isBranchRole(role) ? body.branchId : null,
    mobile: body.mobile.trim(),
  };
}

export function shapeEmailChange(body: Record<string, unknown>) {
  return text(body.email, 254) ? { email: body.email.trim() } : null;
}

export const parseAcknowledgement = (value: unknown) =>
  isRecord(value) && value.accepted === true ? { accepted: true as const } : null;

const conflictMessages: Record<string, string> = {
  "email-taken": "An account or pending invitation already uses this email.",
  "username-taken": "That username is taken.",
  unavailable: "This invitation is no longer pending. Refresh the list.",
};

export function staffInvitationConflict(code: unknown) {
  return typeof code === "string" && Object.hasOwn(conflictMessages, code)
    ? conflictMessages[code]
    : conflictMessages.unavailable;
}
