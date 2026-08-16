import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError, apiJson, requestApiJson, sessionToken } from "@/lib/server/http/api";
import { accessDenied, confirmedActor } from "@/lib/server/http/staff-relay";
import { hasPersonAccess, parsePerson } from "./contracts";

const PERSON_DEADLINE_MS = 20_000;

export async function relayPerson(request: Request, personId: string) {
  try {
    if (!isUuid(personId)) {
      throw new HttpError(400, "Provide a valid person reference.");
    }
    const token = await sessionToken();
    if (!token) {
      throw new HttpError(401, "Sign in to continue.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(PERSON_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    if (!hasPersonAccess(actor)) {
      return accessDenied(actor, "Profile access unavailable.");
    }
    const result = await requestApiJson(
      `/api/people/${personId}`,
      parsePerson,
      { token, signal },
      PERSON_DEADLINE_MS,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}
