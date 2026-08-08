"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import { type ApiResult, type Page } from "@/lib/api";
import { type Branch } from "@/lib/branches";
import { parseCatalogue } from "@/lib/vehicles/parse";
import {
  catalogueFilterError,
  catalogueHref,
  catalogueQuery,
  trimCatalogueFilters,
} from "@/lib/vehicles/query";
import type { CatalogueFilters, CataloguePage } from "@/lib/vehicles/contracts";
import { useInitialToast } from "./use-initial-toast";
import { useBranchChoices } from "./use-branch-choices";
import { writeAddress } from "@/lib/browser-url";

export type CatalogueDraftSync = "all" | "sort" | "none";

export function useVehicleCatalogue(
  initial: ApiResult<CataloguePage>,
  filters: CatalogueFilters,
  page: number,
  initialBranches: Page<Branch> | null,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [active, setActive] = useState({ filters: trimCatalogueFilters(filters), page });
  const [draft, setDraft] = useState(() => trimCatalogueFilters(filters));
  const [busy, setBusy] = useState(false);
  const [photoVersion, setPhotoVersion] = useState(0);
  const branchChoices = useBranchChoices(
    initialBranches,
    25,
    "Branches could not be refreshed. The last confirmed choices remain available.",
  );
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  useInitialToast(initial.data ? null : initial.message);

  useEffect(
    () => () => {
      controller.current?.abort();
    },
    [],
  );

  async function load(
    nextFilters: CatalogueFilters,
    nextPage: number,
    options?: { syncDraft?: CatalogueDraftSync },
  ): Promise<boolean> {
    if (controller.current) {
      return false;
    }
    const normalized = trimCatalogueFilters(nextFilters);
    const invalid = catalogueFilterError(normalized);
    if (invalid) {
      error(invalid);
      return false;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const query = catalogueQuery(normalized, nextPage);
      const result = await apiFetch(`/api/vehicles?${query}`, parseCatalogue, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted) {
        return false;
      }
      if (!result.data || result.data.page !== nextPage) {
        throw new Error("Catalogue unavailable");
      }
      setSnapshot(result.data);
      setPhotoVersion((current) => current + 1);
      setActive({ filters: normalized, page: nextPage });
      const syncDraft = options?.syncDraft ?? "all";
      if (syncDraft === "all") {
        setDraft(normalized);
      } else if (syncDraft === "sort") {
        setDraft((current) => ({
          ...current,
          sort: normalized.sort,
          direction: normalized.direction,
        }));
      }
      writeAddress(catalogueHref(normalized, nextPage));
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        const message =
          "The catalogue could not be loaded. Your last confirmed vehicles and filters are still shown. Apply your filters again, or reload the page.";
        error(message);
      }
      return false;
    } finally {
      if (!attempt.signal.aborted) {
        setBusy(false);
      }
      if (controller.current === attempt) {
        controller.current = null;
      }
    }
  }

  return {
    snapshot,
    active,
    draft,
    setDraft,
    busy,
    photoVersion,
    load,
    branches: branchChoices.branches,
    branchBusy: branchChoices.busy,
    loadBranches: branchChoices.load,
  };
}
