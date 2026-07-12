import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError } from "@/lib/server/http/api";
import { relayAdministratorCommand } from "./account-administration-relay";
import { shapeSuspension } from "./account-suspension";

export function relayAccountSuspension(request: Request, id: string, lifting: boolean) {
  if (!isUuid(id)) {
    return Promise.resolve(apiError(new HttpError(400, "Choose a valid account.")));
  }
  return relayAdministratorCommand(request, {
    path: `/api/accounts/${id}/suspensions${lifting ? "/lift" : ""}`,
    method: "POST",
    shape: (body) => shapeSuspension(body, lifting),
    conflict: () =>
      lifting
        ? "This account is not suspended. Refresh the list."
        : "This account cannot be suspended: it is already suspended or deactivated, it is your own, or it is the last active Administrator.",
  });
}
