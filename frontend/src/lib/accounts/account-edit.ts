import { isUuid } from "@/lib/auth/contracts";

// Staff have no address; a customer's draft always carries one.
export interface AccountEditDraft {
  operationId: string;
  reason: string;
  name: string;
  mobile: string;
  address: string | null;
  email: string;
  requirePasswordReset: boolean;
  requireEmailVerification: boolean;
}

export interface AccountEditSubject {
  name: string;
  mobile: string;
  address: string | null;
  email: string;
}

// Verification starts on and a reset off; both apply only when the email changes.
export const startEdit = (subject: AccountEditSubject): AccountEditDraft => ({
  operationId: crypto.randomUUID(),
  reason: "",
  ...subject,
  requirePasswordReset: false,
  requireEmailVerification: true,
});

export function isEdited(draft: AccountEditDraft, subject: AccountEditSubject) {
  return (
    draft.name.trim() !== subject.name ||
    draft.mobile.trim() !== subject.mobile ||
    (draft.address?.trim() ?? null) !== subject.address ||
    draft.email.trim() !== subject.email
  );
}

export function shapeEdit(body: Record<string, unknown>) {
  const text = (value: unknown) => (typeof value === "string" ? value.trim() : "");
  const reason = text(body.reason);
  const address = body.address === null ? null : text(body.address);
  if (
    !isUuid(body.operationId) ||
    typeof body.requirePasswordReset !== "boolean" ||
    typeof body.requireEmailVerification !== "boolean" ||
    reason.length === 0 ||
    reason.length > 1000 ||
    text(body.name).length === 0 ||
    text(body.mobile).length === 0 ||
    text(body.email).length === 0 ||
    address === ""
  ) {
    return null;
  }
  return {
    operationId: body.operationId,
    reason,
    name: text(body.name),
    mobile: text(body.mobile),
    address,
    email: text(body.email),
    requirePasswordReset: body.requirePasswordReset,
    requireEmailVerification: body.requireEmailVerification,
  };
}
