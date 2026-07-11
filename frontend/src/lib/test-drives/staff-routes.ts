import "server-only";
import type { CurrentActor } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError } from "@/lib/server/http/api";
import { echoesPage } from "@/lib/pagination";
import { relayGatedRead } from "@/lib/server/http/staff-relay";
import {
  hasTestDriveAccess,
  parseStaffTestDriveDetail,
  parseStaffTestDrivePage,
  parseTestDriveHandlers,
} from "./staff";
import { parseStaffTestDriveHistory } from "./staff-history";

const listKeys = [
  "from",
  "to",
  "page",
  "pageSize",
  "queue",
  "status",
  "branchId",
  "handlerId",
  "handler",
  "registration",
  "vehicle",
  "customer",
  "search",
  "sort",
  "direction",
];

const testDriveAccess = {
  allowed: (actor: CurrentActor) => hasTestDriveAccess(actor.role),
  denial: "Test-drive staff access required.",
};

export function relayStaffTestDriveList(request: Request) {
  return relayGatedRead(request, {
    ...testDriveAccess,
    path: "/api/staff/test-drives",
    keys: listKeys,
    parse: (value, query) => echoesPage(parseStaffTestDrivePage(value), query),
  });
}

export function relayStaffTestDriveDetail(request: Request, id: string) {
  if (!isUuid(id)) {
    return Promise.resolve(apiError(new HttpError(400, "Choose a valid test drive.")));
  }

  return relayGatedRead(request, {
    ...testDriveAccess,
    path: `/api/staff/test-drives/${id}`,
    parse: (value) => {
      const detail = parseStaffTestDriveDetail(value);
      return detail?.booking.id === id ? detail : null;
    },
  });
}

export function relayStaffTestDriveHandlers(request: Request, id: string) {
  if (!isUuid(id)) {
    return Promise.resolve(apiError(new HttpError(400, "Choose a valid test drive.")));
  }

  return relayGatedRead(request, {
    ...testDriveAccess,
    path: `/api/staff/test-drives/${id}/handlers`,
    parse: parseTestDriveHandlers,
  });
}

export function relayStaffTestDriveHistory(request: Request, id: string) {
  if (!isUuid(id)) {
    return Promise.resolve(apiError(new HttpError(400, "Choose a valid test drive.")));
  }

  return relayGatedRead(request, {
    ...testDriveAccess,
    path: `/api/staff/test-drives/${id}/history`,
    keys: ["page", "pageSize"],
    parse: (value) => {
      const history = parseStaffTestDriveHistory(value);
      return history?.bookingId === id ? history : null;
    },
  });
}
