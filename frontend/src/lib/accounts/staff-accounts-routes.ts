import "server-only";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError } from "@/lib/server/http/api";
import { echoesPage } from "@/lib/pagination";
import { relayGatedRead } from "@/lib/server/http/staff-relay";
import { administratorAccess } from "./account-administration-relay";
import { accountHistoryPath, parseAccountHistory } from "./account-history";
import { parseStaffAccountPage } from "./staff-accounts";

const listKeys = [
  "view",
  "search",
  "role",
  "branchId",
  "name",
  "email",
  "mobile",
  "page",
  "pageSize",
];

export function relayStaffAccounts(request: Request) {
  return relayGatedRead(request, {
    dataFirst: true,
    ...administratorAccess,
    path: "/api/staff-accounts",
    keys: listKeys,
    parse: (value, query) => echoesPage(parseStaffAccountPage(value), query),
  });
}

export function relayAccountHistory(request: Request, id: string) {
  if (!isUuid(id)) {
    return Promise.resolve(apiError(new HttpError(400, "Choose a valid account.")));
  }

  return relayGatedRead(request, {
    dataFirst: true,
    ...administratorAccess,
    path: accountHistoryPath(id),
    keys: ["page", "pageSize"],
    parse: (value) => {
      const history = parseAccountHistory(value);
      return history?.accountId === id ? history : null;
    },
  });
}
