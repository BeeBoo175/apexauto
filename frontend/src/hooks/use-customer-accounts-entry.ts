"use client";

import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import { apiReadError } from "@/lib/api";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import {
  customerAccountParams,
  customerAccountQuery,
  parseCustomerAccountPage,
} from "@/lib/accounts/customer-accounts";

export function useCustomerAccountsEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry(async (signal) => {
    const query = customerAccountParams(parameters);
    const initial = await privateApiFetch(
      `/api/customer-accounts?${customerAccountQuery(query.filters, query.page)}`,
      parseCustomerAccountPage,
      { signal },
    );
    if (
      !initial.data ||
      initial.data.page !== query.page ||
      initial.data.pageSize !== query.filters.pageSize ||
      actorIdentity(initial.data.actor) !== actorIdentity(actor)
    ) {
      throw apiReadError("Customer accounts unavailable", initial);
    }
    return { ...query, initial: initial.data };
  }, "Customer accounts could not be loaded. Retry to open the workspace.");
}
