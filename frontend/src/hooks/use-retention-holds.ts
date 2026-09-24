"use client";

import { useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { useSectionRead } from "./use-section-read";
import { privateApiFetch } from "@/lib/api-browser";
import {
  holdsPath,
  holdUntilInstant,
  parseRetentionHoldChange,
  parseRetentionHolds,
  validHoldDraft,
  type RetentionHold,
  type RetentionHoldList,
} from "@/lib/settings/retention-holds";

const emptyDraft = { reservationNumber: "", until: "", reason: "" };

// The Administrator's preservation holds: one bounded read, then place or release by reservation number. A hold
// change is idempotent, so an unconfirmed result is settled by reading the list again and repeating if needed.
export function useRetentionHolds() {
  const toast = useToast();
  const [refreshes, setRefreshes] = useState(0);
  const [busy, setBusy] = useState(false);
  const [draft, setDraft] = useState(emptyDraft);
  const inFlight = useRef(false);
  const section = useSectionRead<RetentionHoldList>({
    read: async (signal) => {
      const result = await privateApiFetch(holdsPath, parseRetentionHolds, { signal });
      if (result.status !== 200 || !result.data) {
        throw new Error(result.message);
      }
      return result.data;
    },
    key: "retention-holds",
    refreshes,
    failure: "Preservation holds could not be loaded.",
  });
  const reload = () => setRefreshes((value) => value + 1);

  async function change(reservationNumber: string, until: string | null, reason: string | null) {
    if (inFlight.current) {
      return false;
    }
    inFlight.current = true;
    setBusy(true);
    try {
      const result = await privateApiFetch(holdsPath, parseRetentionHoldChange, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ reservationNumber, until, reason }),
      });
      if (result.status === 200 && result.data) {
        toast.success(until ? "Preservation hold placed." : "Preservation hold released.");
        reload();
        return true;
      }
      if (result.status === 404) {
        toast.error("No reservation has that number.");
      } else if (result.uncertain || result.status >= 500) {
        toast.warning("The change is unconfirmed. Reload the holds before repeating it.");
        reload();
      } else {
        toast.error(result.message || "The hold could not be changed.");
      }
      return false;
    } catch {
      toast.warning("The change is unconfirmed. Reload the holds before repeating it.");
      reload();
      return false;
    } finally {
      inFlight.current = false;
      setBusy(false);
    }
  }

  async function place() {
    if (!validHoldDraft(draft)) {
      return;
    }
    const placed = await change(
      draft.reservationNumber.trim(),
      holdUntilInstant(draft.until),
      draft.reason.trim(),
    );
    if (placed) {
      setDraft(emptyDraft);
    }
  }

  return {
    holds: section.data?.holds ?? null,
    total: section.data?.total ?? 0,
    failed: section.failed,
    busy: busy || section.busy,
    draft,
    valid: validHoldDraft(draft),
    reload,
    edit: (patch: Partial<typeof emptyDraft>) => setDraft((current) => ({ ...current, ...patch })),
    place,
    release: (hold: RetentionHold) => change(hold.reservationNumber, null, null),
  };
}
