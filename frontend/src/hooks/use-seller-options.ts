"use client";

import { useEffect, useState } from "react";
import { apiFetch } from "@/lib/api-browser";
import type { Page } from "@/lib/api";
import { readBranchChoices } from "@/lib/branch-choices";
import type { Branch } from "@/lib/branches";
import type { Taxonomy } from "@/lib/intake/contracts";
import { parseTaxonomy } from "@/lib/intake/parse";

export interface SellerOptions {
  taxonomy: Taxonomy;
  branches: Page<Branch>;
}

// The vehicle choices and branches the seller journey needs, read when a staff member opens an assisted intake.
export function useSellerOptions(enabled: boolean) {
  const [options, setOptions] = useState<SellerOptions | null>(null);
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (!enabled || options) {
      return;
    }
    const controller = new AbortController();
    void Promise.all([
      apiFetch("/api/intake/taxonomy", parseTaxonomy, { signal: controller.signal }),
      readBranchChoices(1, controller.signal, 25),
    ])
      .then(([taxonomy, branches]) => {
        if (controller.signal.aborted) {
          return;
        }
        if (taxonomy.data && branches.data) {
          setOptions({ taxonomy: taxonomy.data, branches: branches.data });
          return;
        }
        setFailed(true);
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setFailed(true);
        }
      });
    return () => controller.abort();
  }, [enabled, options, attempt]);

  function retry() {
    setFailed(false);
    setAttempt((current) => current + 1);
  }

  return { options, failed, retry };
}
