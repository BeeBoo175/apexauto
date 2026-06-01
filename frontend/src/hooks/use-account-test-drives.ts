"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import type { ApiResult } from "@/lib/api";
import { dateRangeError, type HistoryFilters } from "@/lib/intake/list-query";
import { testDriveHistoryQuery, type TestDriveHistory } from "@/lib/test-drives/account";
import { parseTestDriveHistory, parseTestDriveCancellation } from "@/lib/test-drives/customer";
import { useInitialToast } from "./use-initial-toast";

export function useAccountTestDrives(
  initial: ApiResult<TestDriveHistory>,
  initialPage: number,
  initialFilters: HistoryFilters,
  actorId: string,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [page, setPage] = useState(initialPage);
  const [filters, setFilters] = useState(initialFilters);
  const [draft, setDraft] = useState(initialFilters);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  useInitialToast(initial.message);
  useEffect(() => () => controller.current?.abort(), []);

  async function load(nextPage = page, nextFilters = filters) {
    const invalid = dateRangeError(nextFilters);
    if (invalid) {
      toast.error(invalid);
      return;
    }
    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await privateApiFetch(
        `/api/account/test-drives?${testDriveHistoryQuery(nextFilters, nextPage)}`,
        parseTestDriveHistory,
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (
        result.status !== 200 ||
        !result.data ||
        result.data.page !== nextPage ||
        result.data.actor.userId !== actorId
      ) {
        throw new Error("Test-drive history unavailable");
      }
      setSnapshot(result.data);
      setPage(nextPage);
      setFilters(nextFilters);
      const url = new URL(window.location.href);
      url.searchParams.set("testDrivePage", String(nextPage));
      for (const [key, value] of [
        ["testDriveFrom", nextFilters.from],
        ["testDriveTo", nextFilters.to],
      ]) {
        if (value) {
          url.searchParams.set(key, value);
        } else {
          url.searchParams.delete(key);
        }
      }
      window.history.replaceState(null, "", `${url.pathname}${url.search}${url.hash}`);
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "Test drives could not be refreshed. Your last confirmed records remain shown.",
        );
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  function cancelled(receipt: NonNullable<ReturnType<typeof parseTestDriveCancellation>>) {
    controller.current?.abort();
    setSnapshot(
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
    void load();
  }

  return { snapshot, page, filters, draft, setDraft, busy, load, cancelled };
}
