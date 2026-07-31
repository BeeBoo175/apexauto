"use client";

import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import { apiReadError } from "@/lib/api";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import {
  parseStaffAccountPage,
  staffAccountParams,
  staffAccountQuery,
} from "@/lib/accounts/staff-accounts";
import { readBranchChoices } from "@/lib/branch-choices";

export function useStaffAccountsEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry(async (signal) => {
    const query = staffAccountParams(parameters);
    const [initial, branches] = await Promise.all([
      privateApiFetch(
        `/api/staff-accounts?${staffAccountQuery(query.filters, query.page)}`,
        parseStaffAccountPage,
        { signal },
      ),
      readBranchChoices(1, signal, 25).catch(() => null),
    ]);
    if (
      !initial.data ||
      initial.data.page !== query.page ||
      initial.data.pageSize !== query.filters.pageSize ||
      actorIdentity(initial.data.actor) !== actorIdentity(actor)
    ) {
      throw apiReadError("Staff accounts unavailable", initial);
    }
    return { ...query, initial: initial.data, branches: branches?.data ?? null };
  }, "Staff accounts could not be loaded. Retry to open the workspace.");
}
