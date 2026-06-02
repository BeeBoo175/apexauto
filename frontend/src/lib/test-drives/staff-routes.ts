import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import type { CurrentActor } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { DEFAULT_PAGE_SIZE } from "@/lib/pagination";
import { apiError, apiJson, privateApiRead } from "@/lib/server/http/api";
import { accessDenied, allowedQuery, confirmedActor } from "@/lib/server/http/staff-relay";
import {
  hasTestDriveAccess,
  parseStaffTestDriveDetail,
  parseStaffTestDrivePage,
  parseTestDriveHandlers,
} from "./staff";

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
  "search",
  "sort",
  "direction",
];

export async function relayStaffTestDriveList(request: Request) {
  return relayRead(
    request,
    "",
    (value) => {
      const page = parseStaffTestDrivePage(value);
      const query = new URL(request.url).searchParams;
      return page?.page === Number(query.get("page") ?? 1) &&
        page.pageSize === Number(query.get("pageSize") ?? DEFAULT_PAGE_SIZE)
        ? page
        : null;
    },
    listKeys,
  );
}

export async function relayStaffTestDriveDetail(request: Request, id: string) {
  if (!isUuid(id)) {
    return apiError(new HttpError(400, "Choose a valid test drive."));
  }
  return relayRead(request, `/${id}`, (value) => {
    const detail = parseStaffTestDriveDetail(value);
    return detail?.booking.id === id ? detail : null;
  });
}

export async function relayStaffTestDriveHandlers(request: Request, id: string) {
  if (!isUuid(id)) {
    return apiError(new HttpError(400, "Choose a valid test drive."));
  }
  return relayRead(request, `/${id}/handlers`, parseTestDriveHandlers);
}

async function relayRead<T extends { actor: CurrentActor }>(
  request: Request,
  resource: string,
  parse: (value: unknown) => T | null,
  keys: string[] = [],
) {
  try {
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    if (!hasTestDriveAccess(actor.role)) {
      return accessDenied(actor, "Test-drive staff access required.");
    }
    const query = allowedQuery(new URL(request.url).searchParams, keys);
    const suffix = query.size > 0 ? `?${query}` : "";
    const result = await privateApiRead(
      `/api/staff/test-drives${resource}${suffix}`,
      parse,
      signal,
    );
    return apiJson({ ...result, actor: result.data?.actor ?? actor });
  } catch (error) {
    return apiError(error);
  }
}
