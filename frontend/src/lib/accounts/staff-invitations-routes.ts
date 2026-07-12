import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError } from "@/lib/server/http/api";
import { relayAdministratorCommand } from "./account-administration-relay";
import { shapeEmailChange, shapeInvitation } from "./staff-invitations";

const conflictMessages: Record<string, string> = {
  EmailTaken: "An account or pending invitation already uses this email.",
  UsernameTaken: "That username is taken.",
  Unavailable: "This invitation is no longer pending. Refresh the list.",
};

const conflict = (code: unknown) =>
  (typeof code === "string" && conflictMessages[code]) || conflictMessages.Unavailable;

export const relayInvite = (request: Request) =>
  relayAdministratorCommand(request, {
    path: "/api/staff-invitations",
    method: "POST",
    shape: shapeInvitation,
    conflict,
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
      conflict,
    });
  }
  return relayAdministratorCommand(request, {
    path: `${path}/${command}`,
    method: "POST",
    shape: null,
    conflict,
  });
}
