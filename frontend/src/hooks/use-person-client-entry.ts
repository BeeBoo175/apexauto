"use client";

import { useStaffClientEntry } from "./use-staff-client-entry";
import { parsePerson, type Person } from "@/lib/account/contracts";
import { privateApiFetch } from "@/lib/api-browser";

export function usePersonClientEntry(id: string) {
  return useStaffClientEntry<Person>(async (signal) => {
    const result = await privateApiFetch(`/api/people/${id}`, parsePerson, { signal });
    signal.throwIfAborted();
    if (result.status !== 200 || result.data?.userId !== id) {
      throw new Error("Person unavailable");
    }
    return result.data;
  }, "The profile could not be loaded. Retry to open it.");
}
