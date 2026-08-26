import "server-only";

import { API_DEADLINE_MS } from "@/lib/api";
import { readCurrentActor } from "@/lib/auth/server";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError, readBoundedJson } from "@/lib/http";
import { requireSameOrigin } from "@/lib/server/http/request";
import { requireJsonRequest } from "@/lib/server/http/staff-relay";
import {
  apiError,
  apiJson,
  privateApiRead,
  requestApiJson,
  sessionToken,
} from "@/lib/server/http/api";
import {
  parseBankAccountRequest,
  parseDepositRefundList,
  parseDepositRefundReceipt,
} from "./account-refunds";

async function customerActor(signal: AbortSignal) {
  const current = await readCurrentActor(signal);
  if (!current.actor) {
    throw new HttpError(current.status === 401 ? 401 : 502, "Your session could not be confirmed.");
  }

  if (current.actor.role !== "Customer") {
    throw new HttpError(403, "Customer account required.");
  }

  return current.actor;
}

export async function relayDepositRefunds(request: Request) {
  try {
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const actor = await customerActor(signal);
    const result = await privateApiRead("/api/reservation-refunds", parseDepositRefundList, signal);
    return apiJson({ ...result, actor });
  } catch (error) {
    return apiError(error);
  }
}

// The account number goes straight to the provider through the API and is never kept by the page or the relay.
export async function relayBankAccount(request: Request, paymentRequestId: string) {
  try {
    requireSameOrigin(request);
    if (!isUuid(paymentRequestId)) {
      throw new HttpError(400, "Choose a valid deposit.");
    }

    requireJsonRequest(request);

    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(API_DEADLINE_MS)]);
    const body = parseBankAccountRequest(await readBoundedJson(request.body, signal, 4 * 1024));
    if (!body) {
      throw new HttpError(400, "Provide a bank, a numeric account number and the account holder.");
    }

    const actor = await customerActor(signal);
    const result = await requestApiJson(
      `/api/reservation-refunds/${paymentRequestId}/bank-account`,
      parseDepositRefundReceipt,
      { token: await sessionToken(), signal, method: "POST", body },
    );
    return apiJson({ ...result, actor });
  } catch (error) {
    if (error instanceof SyntaxError) {
      return apiError(new HttpError(400, "Provide valid JSON details."));
    }

    return apiError(error);
  }
}
