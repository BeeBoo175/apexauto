import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import { apiError, apiJson, privateApiRead } from "@/lib/server/http/api";
import { accessDenied, allowedQuery, confirmedActor } from "@/lib/server/http/staff-relay";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { accountHistoryPath, parseAccountHistory } from "./account-history";
import { hasAccountAdministration, parseStaffAccountPage } from "./staff-accounts";

const listKeys = ["tab", "search", "role", "branchId", "page", "pageSize"];

export async function relayStaffAccounts(request: Request) {
  try {
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    if (!hasAccountAdministration(actor)) {
      return accessDenied(actor, "Administrator access required.");
    }
    const query = allowedQuery(new URL(request.url).searchParams, listKeys);
    const result = await privateApiRead(
      `/api/staff-accounts${query.size > 0 ? `?${query}` : ""}`,
      (value) => {
        const page = parseStaffAccountPage(value);
        return page?.page === Number(query.get("page") ?? 1) &&
          page.pageSize === Number(query.get("pageSize") ?? DEFAULT_PAGE_SIZE)
          ? page
          : null;
      },
      signal,
    );
    return apiJson({ ...result, actor: result.data?.actor ?? actor });
  } catch (error) {
    return apiError(error);
  }
}

export async function relayAccountHistory(request: Request, id: string) {
  try {
    if (!isUuid(id)) {
      throw new HttpError(400, "Choose a valid account.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    if (!hasAccountAdministration(actor)) {
      return accessDenied(actor, "Administrator access required.");
    }
    const query = allowedQuery(new URL(request.url).searchParams, ["page", "pageSize"]);
    const result = await privateApiRead(
      `${accountHistoryPath(id)}${query.size > 0 ? `?${query}` : ""}`,
      (value) => {
        const history = parseAccountHistory(value);
        return history?.accountId === id ? history : null;
      },
      signal,
    );
    return apiJson({ ...result, actor: result.data?.actor ?? actor });
  } catch (error) {
    return apiError(error);
  }
}
