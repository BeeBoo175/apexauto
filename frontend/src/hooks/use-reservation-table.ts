"use client";

import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { useInitialToast } from "./use-initial-toast";
import { apiFetch, privateApiFetch } from "@/lib/api-browser";
import { parsePage, type ApiResult, type Page } from "@/lib/api";
import { parseBranch, type Branch } from "@/lib/branches";
import {
  reservationQuery,
  type ReservationFilters,
  type ReservationPage,
} from "@/lib/vehicles/reservations";
import { parseReservationSnapshot } from "@/lib/vehicles/parse-reservations";

export function useReservationTable(
  initial: ApiResult<ReservationPage>,
  filters: ReservationFilters,
  page: number,
  initialBranches: Page<Branch> | null,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [active, setActive] = useState({ filters, page });
  const [draft, setDraft] = useState(filters);
  const [branches, setBranches] = useState(initialBranches);
  const [busy, setBusy] = useState(false);
  const [pendingView, setPendingView] = useState<boolean | null>(null);
  const [ready, setReady] = useState(false);
  const [revision, setRevision] = useState(0);
  const [entry, setEntry] = useState(initial);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  useInitialToast(initial.message);

  if (entry !== initial) {
    setEntry(initial);
    setBusy(false);
    setPendingView(null);
    if (initial.data) {
      setSnapshot(initial.data);
      setActive({ filters, page });
      setDraft(filters);
      setRevision((value) => value + 1);
    }
  }

  useEffect(() => {
    queueMicrotask(() => setReady(true));
  }, []);

  useLayoutEffect(() => {
    return () => {
      controller.current?.abort();
      controller.current = null;
    };
  }, [initial]);

  async function load(nextFilters = active.filters, nextPage = active.page) {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    const submitted = draft;
    controller.current = attempt;
    setBusy(true);
    setPendingView(nextFilters.isActive === active.filters.isActive ? null : nextFilters.isActive);
    try {
      const result = await privateApiFetch(
        `/api/staff/reservations?${reservationQuery(nextFilters, nextPage)}`,
        parseReservationSnapshot,
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      if (
        result.status !== 200 ||
        !result.data ||
        result.data.page !== nextPage ||
        result.data.pageSize !== nextFilters.pageSize ||
        result.data.items.some((row) => (row.status === "Active") !== nextFilters.isActive)
      ) {
        throw new Error("Reservations unavailable");
      }
      setSnapshot(result.data);
      setActive({ filters: nextFilters, page: nextPage });
      setDraft((current) => ({
        ...nextFilters,
        branchId: current.branchId === submitted.branchId ? nextFilters.branchId : current.branchId,
        search:
          current.search === submitted.search && nextFilters.search !== active.filters.search
            ? nextFilters.search
            : current.search,
      }));
      setRevision((value) => value + 1);
      const url = new URL(window.location.href);
      const selected = url.searchParams.get("reservation");
      url.search = reservationQuery(nextFilters, nextPage);
      if (selected) {
        url.searchParams.set("reservation", selected);
      }
      window.history.replaceState(null, "", url);
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "Reservations could not be refreshed. The last confirmed rows and filters remain shown.",
        );
      }
      return false;
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
        setPendingView(null);
      }
    }
  }

  async function loadBranches(nextPage: number) {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await apiFetch(
        `/api/intake/branches?page=${nextPage}`,
        (value) => parsePage(value, parseBranch),
        { signal: attempt.signal },
      );
      if (!attempt.signal.aborted && controller.current === attempt && result.data) {
        setBranches(result.data);
      }
    } catch {
      if (!attempt.signal.aborted) {
        toast.error("Branch choices could not be refreshed. Existing choices remain shown.");
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  function refresh() {
    controller.current?.abort();
    controller.current = null;
    return load();
  }

  return {
    snapshot,
    active,
    draft,
    setDraft,
    branches,
    busy,
    pendingView,
    ready,
    revision,
    load,
    refresh,
    loadBranches,
  };
}
