import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError } from "@/lib/server/http/api";
import { relayAdministratorCommand } from "./account-administration-relay";
import { shapeEmailChange, shapeInvitation, staffInvitationConflict } from "./staff-invitations";

export const relayInvite = (request: Request) =>
  relayAdministratorCommand(request, {
    path: "/api/staff-invitations",
    method: "POST",
    shape: shapeInvitation,
    conflict: staffInvitationConflict,
  });

export function relayInvitationCommand(
  request: Request,
  id: string,
  command: "resends" | "email" | "revocations",
) {
  if (!isUuid(id)) {
    return Promise.resolve(apiError(new HttpError(400, "Choose a valid invitation.")));
  }
  const path = `/api/staff-invitations/${id}`;
  if (command === "email") {
    return relayAdministratorCommand(request, {
      path: `${path}/email`,
      method: "PUT",
      shape: shapeEmailChange,
      conflict: staffInvitationConflict,
    });
  }
  return relayAdministratorCommand(request, {
    path: `${path}/${command}`,
    method: "POST",
    shape: null,
    conflict: staffInvitationConflict,
  });
}
