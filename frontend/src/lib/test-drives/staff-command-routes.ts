import "server-only";
import { API_DEADLINE_MS } from "@/lib/api";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError, readBoundedJson } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import { apiError, apiJson, requestApiJson, sessionToken } from "@/lib/server/http/api";
import { accessDenied, confirmedActor, requireJsonRequest } from "@/lib/server/http/staff-relay";
import { parseTestDriveConflict } from "./errors";
import {
  parseTestDriveStaffReceipt,
  shapeTestDriveStaffCommand,
  type TestDriveStaffCommand,
} from "./staff-commands";

export async function relayStaffTestDriveCommand(
  request: Request,
  bookingId: string,
  command: TestDriveStaffCommand,
) {
  try {
    requireSameOrigin(request);
    requireJsonRequest(request);
    if (!isUuid(bookingId)) {
      throw new HttpError(400, "Choose a valid test drive.");
    }
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await confirmedActor(signal);
    if (
      !["SalesAgent", "BranchManager"].includes(actor.role) ||
      (command === "assignments" && actor.role !== "BranchManager")
    ) {
      return accessDenied(actor, "This account cannot perform this test-drive action.");
    }
    const body = shapeTestDriveStaffCommand(
      command,
      await readBoundedJson(request.body, signal, 8 * 1024),
    );
    if (!body) {
      throw new HttpError(
        400,
        "Provide valid operation, ownership and attendance details for this action.",
      );
    }
    const result = await requestApiJson(
      `/api/staff/test-drives/${bookingId}/${command}`,
      (value) => parseTestDriveStaffReceipt(command, value, body, bookingId, actor),
      { token: await sessionToken(), signal, method: "POST", body },
      API_DEADLINE_MS,
      undefined,
      parseTestDriveConflict,
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(
      error instanceof SyntaxError ? new HttpError(400, "Provide valid JSON details.") : error,
    );
  }
}
