"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import { type ApiResult, type Page } from "@/lib/api";
import { type Branch } from "@/lib/branches";
import { parseCatalogue } from "@/lib/vehicles/parse";
import { catalogueFilterError, catalogueQuery } from "@/lib/vehicles/query";
import type { CatalogueFilters, CataloguePage } from "@/lib/vehicles/contracts";
import { useInitialToast } from "./use-initial-toast";
import { useBranchChoices } from "./use-branch-choices";
import { writeAddress } from "@/lib/browser-url";

export function useVehicleCatalogue(
  initial: ApiResult<CataloguePage>,
  filters: CatalogueFilters,
  page: number,
  initialBranches: Page<Branch> | null,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [active, setActive] = useState({ filters, page });
  const [draft, setDraft] = useState(filters);
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

  async function load(nextFilters: CatalogueFilters, nextPage: number) {
    if (controller.current) {
      return;
    }
    const invalid = catalogueFilterError(nextFilters);
    if (invalid) {
      error(invalid);
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const query = catalogueQuery(nextFilters, nextPage);
      const result = await apiFetch(`/api/vehicles?${query}`, parseCatalogue, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted) {
        return;
      }
      if (!result.data || result.data.page !== nextPage) {
        throw new Error("Catalogue unavailable");
      }
      setSnapshot(result.data);
      setPhotoVersion((current) => current + 1);
      setActive({ filters: nextFilters, page: nextPage });
      writeAddress(`/vehicles?${query}`);
    } catch {
      if (!attempt.signal.aborted) {
        const message =
          "The catalogue could not be loaded. Your last confirmed vehicles and filters are still shown. Apply your filters again, or reload the page.";
        error(message);
      }
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
