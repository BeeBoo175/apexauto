import { isUuid } from "@/lib/auth/contracts";

export interface AccountSuspensionDraft {
  operationId: string;
  reason: string;
  requirePasswordReset: boolean;
  requireEmailVerification: boolean;
}

export const emptySuspension = (): AccountSuspensionDraft => ({
  operationId: crypto.randomUUID(),
  reason: "",
  requirePasswordReset: false,
  requireEmailVerification: false,
});

// Suspending needs a reason; lifting records none. Both carry the same two independent security choices.
export function shapeSuspension(body: Record<string, unknown>, lifting: boolean) {
  const reason = typeof body.reason === "string" ? body.reason.trim() : "";
  if (
    !isUuid(body.operationId) ||
    typeof body.requirePasswordReset !== "boolean" ||
    typeof body.requireEmailVerification !== "boolean" ||
    (!lifting && (reason.length === 0 || reason.length > 1000))
  ) {
    return null;
  }
  return {
    operationId: body.operationId,
    reason: lifting ? null : reason,
    requirePasswordReset: body.requirePasswordReset,
    requireEmailVerification: body.requireEmailVerification,
  };
}
