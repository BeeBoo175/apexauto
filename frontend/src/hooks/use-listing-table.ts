"use client";

import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { useInitialToast } from "./use-initial-toast";
import { apiFetch } from "@/lib/api-browser";
import { parsePage, type ApiResult, type Page } from "@/lib/api";
import { parseBranch, type Branch } from "@/lib/branches";
import { listingQuery, type ListingFilters, type ListingItem } from "@/lib/vehicles/listings";
import { parseListings } from "@/lib/vehicles/parse-listings";

export function useListingTable(
  initial: ApiResult<Page<ListingItem>>,
  filters: ListingFilters,
  page: number,
  initialBranches: Page<Branch> | null,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [active, setActive] = useState({ filters, page });
  const [draft, setDraft] = useState(filters);
  const [branches, setBranches] = useState(initialBranches);
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const [revision, setRevision] = useState(0);
  const [entry, setEntry] = useState(initial);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  useInitialToast(initial.message);

  if (entry !== initial) {
    setEntry(initial);
    setBusy(false);
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
    // A new server entry supersedes reads before their completions can replace its snapshot.
    return () => {
      controller.current?.abort();
      controller.current = null;
    };
  }, [initial]);

  async function load(nextFilters = active.filters, nextPage = active.page, resetDraft = false) {
    if (controller.current) {
      return;
    }
    if (
      nextFilters.search.length > 160 ||
      /[\u0000-\u001f\u007f-\u009f]/u.test(nextFilters.search)
    ) {
      toast.error("Search must be at most 160 characters without control characters.");
      return;
    }
    const attempt = new AbortController();
    const submitted = draft;
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await apiFetch(
        `/api/staff/listings?${listingQuery(nextFilters, nextPage)}`,
        parseListings,
        { signal: attempt.signal },
        { requireActor: true },
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (
        !result.data ||
        result.status !== 200 ||
        result.data.page !== nextPage ||
        result.data.pageSize !== nextFilters.pageSize
      ) {
        throw new Error("Listings unavailable");
      }
      setSnapshot(result.data);
      setActive({ filters: nextFilters, page: nextPage });
      setDraft((current) => ({
        ...current,
        search:
          current.search === submitted.search &&
          (resetDraft || nextFilters.search !== active.filters.search)
            ? nextFilters.search
            : current.search,
        branchId:
          current.branchId === submitted.branchId &&
          (resetDraft || nextFilters.branchId !== active.filters.branchId)
            ? nextFilters.branchId
            : current.branchId,
        pageSize: nextFilters.pageSize,
        sort: nextFilters.sort,
        direction: nextFilters.direction,
      }));
      setRevision((value) => value + 1);
      const query = new URLSearchParams(window.location.search);
      for (const key of ["page", "pageSize", "search", "branchId", "sort", "direction"]) {
        query.delete(key);
      }
      new URLSearchParams(listingQuery(nextFilters, nextPage)).forEach((value, key) =>
        query.set(key, value),
      );
      window.history.replaceState(null, "", `/dashboard/management?${query}`);
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "Listings could not be refreshed. The last confirmed rows and filters remain shown.",
        );
      }
      return false;
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
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
      if (!attempt.signal.aborted && result.data) {
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

  return { snapshot, active, draft, setDraft, branches, busy, ready, revision, load, loadBranches };
}
