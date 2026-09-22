"use client";

import { useEffect, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import type { SlotOffer } from "@/lib/intake/contracts";
import { parseSlots } from "@/lib/intake/parse";

// Appraisal bays still open at one showroom on one day. The last confirmed offer stays visible if a refresh fails.
export function useShowroomSlots(branchId: string, date: string) {
  const [offer, setOffer] = useState<SlotOffer | null>(null);
  const [busy, setBusy] = useState(false);
  const { error: showError } = useToast();

  useEffect(() => {
    const attempt = new AbortController();
    async function load() {
      setBusy(true);
      try {
        const result = await apiFetch(
          `/api/intake/slots?branchId=${encodeURIComponent(branchId)}&date=${encodeURIComponent(date)}`,
          parseSlots,
          { signal: attempt.signal },
        );
        if (attempt.signal.aborted) {
          return;
        }
        if (result.status !== 200 || !result.data) {
          throw new Error(result.message);
        }
        setOffer(result.data);
      } catch (error) {
        if (!attempt.signal.aborted) {
          showError(
            error instanceof Error && error.message
              ? error.message
              : "Times could not be refreshed. The last confirmed times remain visible.",
          );
        }
      } finally {
        if (!attempt.signal.aborted) {
          setBusy(false);
        }
      }
    }
    void load();
    return () => attempt.abort();
  }, [branchId, date, showError]);

  const current = offer?.branchId === branchId && offer.date === date ? offer : null;
  return { offer: current, busy };
}
