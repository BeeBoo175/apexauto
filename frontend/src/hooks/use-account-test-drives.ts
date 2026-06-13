"use client";

import type { ApiResult } from "@/lib/api";
import {
  testDriveHistoryQuery,
  type TestDriveHistory,
  type TestDriveHistoryFilters,
} from "@/lib/test-drives/account";
import { parseTestDriveHistory, parseTestDriveCancellation } from "@/lib/test-drives/customer";
import { useAccountHistory } from "./use-account-history";

export function useAccountTestDrives(
  initial: ApiResult<TestDriveHistory>,
  initialPage: number,
  initialFilters: TestDriveHistoryFilters,
  actorId: string,
) {
  const history = useAccountHistory(initial, initialPage, initialFilters, {
    path: (filters, page) => `/api/account/test-drives?${testDriveHistoryQuery(filters, page)}`,
    parse: parseTestDriveHistory,
    confirms: (confirmed) => confirmed.actor.userId === actorId,
    prefix: "testDrive",
    failureMessage: "Test drives could not be refreshed. Your last confirmed records remain shown.",
  });

  function cancelled(receipt: NonNullable<ReturnType<typeof parseTestDriveCancellation>>) {
    history.abort();
    history.setSnapshot(
      (current) =>
        current && {
          ...current,
          items: current.items.map((item) =>
            item.id === receipt.bookingId
              ? {
                  ...item,
                  status: "Cancelled",
                  cancelledAt: receipt.recordedAt,
                  cancellationReason: receipt.reason,
                }
              : item,
          ),
        },
    );
    void history.load();
  }

  return {
    snapshot: history.snapshot,
    page: history.page,
    filters: history.filters,
    draft: history.draft,
    setDraft: history.setDraft,
    busy: history.busy,
    load: history.load,
    cancelled,
  };
}
