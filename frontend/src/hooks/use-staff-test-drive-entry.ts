"use client";

import { useStaffClientEntry } from "./use-staff-client-entry";
import { privateApiFetch } from "@/lib/api-browser";
import { apiReadError } from "@/lib/api";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { parseStaffTestDrivePage, parseStaffTestDriveDetail } from "@/lib/test-drives/staff";
import {
  staffTestDriveParams,
  staffTestDriveQuery,
  testDriveDateError,
} from "@/lib/test-drives/staff-query";
import { readBranchChoices } from "@/lib/branch-choices";

export function useStaffTestDriveEntry(actor: CurrentActor, parameters: SearchParams) {
  return useStaffClientEntry(async (signal) => {
    const query = staffTestDriveParams(parameters, actor.role === "SalesAgent");
    const notice = testDriveDateError(query.filters);
    if (notice) {
      query.filters.from = "";
      query.filters.to = "";
    }
    const [initial, selected, surface, branches] = await Promise.all([
      privateApiFetch(
        `/api/staff/test-drives?${staffTestDriveQuery(query.filters, query.page)}`,
        parseStaffTestDrivePage,
        { signal },
      ),
      query.selectedId
        ? privateApiFetch(
            `/api/staff/test-drives/${query.selectedId}`,
            (value) => {
              const detail = parseStaffTestDriveDetail(value);
              return detail?.booking.id === query.selectedId ? detail : null;
            },
            { signal },
          ).catch(() => null)
        : null,
      prepareSurface(actor.role === "SalesAgent"),
      actor.role === "GeneralManager" ? readBranchChoices(1, signal, 25).catch(() => null) : null,
    ]);
    if (
      !initial.data ||
      initial.data.page !== query.page ||
      initial.data.pageSize !== query.filters.pageSize ||
      actorIdentity(initial.data.actor) !== actorIdentity(actor)
    ) {
      throw apiReadError("Test drives unavailable", initial);
    }
    if (selected?.data && actorIdentity(selected.data.actor) !== actorIdentity(actor)) {
      throw new Error("Test drive actor changed");
    }
    return {
      ...query,
      notice,
      initial: initial.data,
      selected: selected?.data ?? null,
      branches: branches?.data ?? null,
      surface,
    };
  }, "Test drives could not be loaded. Retry to open the workspace.");
}

async function prepareSurface(agent: boolean) {
  if (agent) {
    const { StaffTestDriveQueue } = await import("@/components/test-drives/StaffTestDriveQueue");
    return { kind: "agent" as const, Component: StaffTestDriveQueue };
  }
  const { StaffTestDriveTable } = await import("@/components/test-drives/StaffTestDriveTable");
  return { kind: "manager" as const, Component: StaffTestDriveTable };
}
