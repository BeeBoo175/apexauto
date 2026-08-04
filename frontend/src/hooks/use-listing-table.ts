"use client";

import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { useInitialToast } from "./use-initial-toast";
import { privateApiFetch } from "@/lib/api-browser";
import { type ApiResult, type Page } from "@/lib/api";
import { type Branch } from "@/lib/branches";
import { listingQuery, type ListingFilters, type ListingItem } from "@/lib/vehicles/listings";
import { parseListings } from "@/lib/vehicles/parse-listings";
import { readBranchChoices } from "@/lib/branch-choices";

export function useListingTable(
  initial: ApiResult<Page<ListingItem>>,
  filters: ListingFilters,
  page: number,
  initialBranches: Page<Branch> | null,
  onLocation: (filters: ListingFilters, page: number, mode: false | "push" | "replace") => void,
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

  // Applied loads push; refresh and reconciliation replace; a Back or Forward destination writes nothing.
  async function load(
    nextFilters = active.filters,
    nextPage = active.page,
    resetDraft = false,
    history: false | "push" | "replace" = "push",
    supersede = false,
  ) {
    if (controller.current && !supersede) {
      return;
    }
    controller.current?.abort();
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
      const result = await privateApiFetch(
        `/api/staff/listings?${listingQuery(nextFilters, nextPage)}`,
        parseListings,
        { signal: attempt.signal },
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
        inspection:
          current.inspection === submitted.inspection ? nextFilters.inspection : current.inspection,
        publication:
          current.publication === submitted.publication
            ? nextFilters.publication
            : current.publication,
        minimumPrice:
          current.minimumPrice === submitted.minimumPrice
            ? nextFilters.minimumPrice
            : current.minimumPrice,
        maximumPrice:
          current.maximumPrice === submitted.maximumPrice
            ? nextFilters.maximumPrice
            : current.maximumPrice,
        minimumPublishedPrice:
          current.minimumPublishedPrice === submitted.minimumPublishedPrice
            ? nextFilters.minimumPublishedPrice
            : current.minimumPublishedPrice,
        maximumPublishedPrice:
          current.maximumPublishedPrice === submitted.maximumPublishedPrice
            ? nextFilters.maximumPublishedPrice
            : current.maximumPublishedPrice,
        from: current.from === submitted.from ? nextFilters.from : current.from,
        to: current.to === submitted.to ? nextFilters.to : current.to,
        registration:
          current.registration === submitted.registration
            ? nextFilters.registration
            : current.registration,
        vehicle: current.vehicle === submitted.vehicle ? nextFilters.vehicle : current.vehicle,
        pageSize: nextFilters.pageSize,
        sort: nextFilters.sort,
        direction: nextFilters.direction,
      }));
      setRevision((value) => value + 1);
      onLocation(nextFilters, nextPage, history);
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
      const result = await readBranchChoices(nextPage, attempt.signal);
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

  function cancel() {
    controller.current?.abort();
    controller.current = null;
    setBusy(false);
  }

  function refreshAfterCommand() {
    cancel();
    void load(undefined, undefined, false, "replace");
  }

  return {
    snapshot,
    active,
    draft,
    setDraft,
    branches,
    busy,
    ready,
    revision,
    load,
    loadBranches,
    refreshAfterCommand,
    cancel,
  };
}
