import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError, apiJson, privateApiRead } from "@/lib/server/http/api";
import { accessDenied, confirmedActor } from "@/lib/server/http/staff-relay";
import { relayAdministratorCommand } from "./account-admin-relay";
import {
  accountBlockersPath,
  parseBlockers,
  shapeAccountCommand,
  type StaffAccountCommandKind,
} from "./account-administration";
import { hasAccountAdministration } from "./staff-accounts";

const conflicts: Record<StaffAccountCommandKind, string> = {
  deactivations:
    "This account cannot be deactivated: it is already deactivated, it is your own, or it is the last active Administrator.",
  reactivations: "This account is not deactivated, or its branch is no longer active.",
  transfers: "This account cannot move to that branch. Refresh and choose an active branch.",
};

export function relayStaffAccountCommand(
  request: Request,
  id: string,
  kind: StaffAccountCommandKind,
) {
  if (!isUuid(id)) {
    return Promise.resolve(apiError(new HttpError(400, "Choose a valid account.")));
  }
  return relayAdministratorCommand(request, {
    path: `/api/staff-accounts/${id}/${kind}`,
    method: "POST",
    shape: (body) => shapeAccountCommand(kind, body),
    conflict: (code) =>
      code === "Blocked"
        ? "This employee still owns open work. Ask the branch manager to resolve it first."
        : conflicts[kind],
  });
}

export async function relayAccountBlockers(request: Request, id: string) {
  try {
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid account.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    if (!hasAccountAdministration(actor)) {
      return accessDenied(actor, "Administrator access required.");
    }
    // The browser parses the same {blockers} body, so the relay forwards it once it checks out.
    const result = await privateApiRead(
      accountBlockersPath(id),
      (value) => (parseBlockers(value) ? (value as { blockers: unknown[] }) : null),
      signal,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}
