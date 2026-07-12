import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError } from "@/lib/server/http/api";
import { echoesPage } from "@/lib/pagination";
import { relayGatedRead } from "@/lib/server/http/staff-relay";
import { administratorAccess } from "./account-administration-relay";
import { parseCustomerAccountDetail, parseCustomerAccountPage } from "./customer-accounts";

const listKeys = ["tab", "search", "name", "email", "mobile", "page", "pageSize"];

export function relayCustomerAccounts(request: Request) {
  return relayGatedRead(request, {
    ...administratorAccess,
    path: "/api/customer-accounts",
    keys: listKeys,
    parse: (value, query) => echoesPage(parseCustomerAccountPage(value), query),
  });
}

export async function relayCustomerAccount(request: Request, id: string) {
  if (!isUuid(id)) {
    return apiError(new HttpError(400, "Choose a valid customer."));
  }

  return relayGatedRead(request, {
    ...administratorAccess,
    path: `/api/customer-accounts/${id}`,
    parse: (value) => {
      const detail = parseCustomerAccountDetail(value);
      return detail?.id === id ? detail : null;
    },
  });
}
