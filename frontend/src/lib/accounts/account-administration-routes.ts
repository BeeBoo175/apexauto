import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError } from "@/lib/server/http/api";
import { relayGatedRead } from "@/lib/server/http/staff-relay";
import { administratorAccess, relayAdministratorCommand } from "./account-admin-relay";
import {
  accountBlockersPath,
  parseBlockers,
  shapeAccountCommand,
  staffAccountConflict,
  type StaffAccountCommandKind,
} from "./account-administration";

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
    conflict: (code) => staffAccountConflict(kind, code),
  });
}

export function relayAccountBlockers(request: Request, id: string) {
  if (!isUuid(id)) {
    return Promise.resolve(apiError(new HttpError(400, "Choose a valid account.")));
  }

  // The browser parses the same {blockers} body, so the relay forwards it once it checks out.
  return relayGatedRead(request, {
    ...administratorAccess,
    path: accountBlockersPath(id),
    parse: (value) => (parseBlockers(value) ? value : null),
  });
}
