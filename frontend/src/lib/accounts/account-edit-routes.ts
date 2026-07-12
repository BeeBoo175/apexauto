import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError } from "@/lib/server/http/api";
import { relayAdministratorCommand } from "./account-administration-relay";
import { shapeEdit } from "./account-edit";

export function relayAccountEdit(request: Request, id: string) {
  if (!isUuid(id)) {
    return Promise.resolve(apiError(new HttpError(400, "Choose a valid account.")));
  }
  return relayAdministratorCommand(request, {
    path: `/api/accounts/${id}/edits`,
    method: "POST",
    shape: shapeEdit,
    conflict: () =>
      "Nothing was changed: the email may belong to another account or invitation, the account may be your own, or no detail differs.",
  });
}
