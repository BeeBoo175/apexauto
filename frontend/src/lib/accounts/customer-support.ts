import { isRecord, isUuid } from "@/lib/auth/contracts";

export const customerSupportActions = {
  "nric-reveal": { upstream: "nric-reveals", done: "Reason recorded." },
  "session-revocation": { upstream: "session-revocations", done: "Sessions ended." },
  "password-reset": { upstream: "password-resets", done: "Reset email sent." },
  "verification-resend": { upstream: "verification-resends", done: "Verification email sent." },
} as const;

export type CustomerSupportAction = keyof typeof customerSupportActions;

export const isCustomerSupportAction = (value: string): value is CustomerSupportAction =>
  Object.hasOwn(customerSupportActions, value);

export interface CustomerSupportDraft {
  operationId: string;
  reason: string;
}

export const emptySupport = (): CustomerSupportDraft => ({
  operationId: crypto.randomUUID(),
  reason: "",
});

// Every support action carries the Administrator's reason and nothing else.
export function shapeSupport(body: Record<string, unknown>) {
  const reason = typeof body.reason === "string" ? body.reason.trim() : "";
  return isUuid(body.operationId) && reason.length > 0 && reason.length <= 1000
    ? { operationId: body.operationId, reason }
    : null;
}

export const parseNricReveal = (value: unknown) =>
  isRecord(value) && typeof value.nric === "string" && value.nric.length > 0
    ? { nric: value.nric }
    : null;
