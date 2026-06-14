"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import type { Page } from "@/lib/api";
import { readBranchChoices } from "@/lib/branch-choices";
import type { Branch } from "@/lib/branches";

// Pages the branch choices independently of any list load: one read at a time, last confirmed page kept on failure.
export function useBranchChoices(
  initial: Page<Branch> | null,
  pageSize: number | undefined,
  failureMessage: string,
) {
  const [branches, setBranches] = useState(initial);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();
  useEffect(() => () => controller.current?.abort(), []);

  async function load(page: number) {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await readBranchChoices(page, attempt.signal, pageSize);
      if (attempt.signal.aborted) {
        return;
      }
      if (!result.data || result.data.page !== page) {
        throw new Error("Branches unavailable");
      }
      setBranches(result.data);
    } catch {
      if (!attempt.signal.aborted) {
        error(failureMessage);
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  return { branches, busy, load };
}
