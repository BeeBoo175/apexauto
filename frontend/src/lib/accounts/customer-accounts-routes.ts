import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import { apiError, apiJson, privateApiRead } from "@/lib/server/http/api";
import { accessDenied, allowedQuery, confirmedActor } from "@/lib/server/http/staff-relay";
import { parseCustomerAccountDetail, parseCustomerAccountPage } from "./customer-accounts";
import { hasAccountAdministration } from "./staff-accounts";

async function relayRead<T extends { actor: unknown }>(
  request: Request,
  path: string,
  parse: (value: unknown) => T | null,
) {
  try {
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    if (!hasAccountAdministration(actor)) {
      return accessDenied(actor, "Administrator access required.");
    }
    const result = await privateApiRead(path, parse, signal);
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}

export async function relayCustomerAccounts(request: Request) {
  const query = allowedQuery(new URL(request.url).searchParams, [
    "tab",
    "search",
    "name",
    "email",
    "mobile",
    "page",
    "pageSize",
  ]);
  return relayRead(
    request,
    `/api/customer-accounts${query.size > 0 ? `?${query}` : ""}`,
    (value) => {
      const page = parseCustomerAccountPage(value);
      return page?.page === Number(query.get("page") ?? 1) &&
        page.pageSize === Number(query.get("pageSize") ?? DEFAULT_PAGE_SIZE)
        ? page
        : null;
    },
  );
}

export async function relayCustomerAccount(request: Request, id: string) {
  if (!isUuid(id)) {
    return apiError(new HttpError(400, "Choose a valid customer."));
  }
  return relayRead(request, `/api/customer-accounts/${id}`, (value) => {
    const detail = parseCustomerAccountDetail(value);
    return detail?.id === id ? detail : null;
  });
}
