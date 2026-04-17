"use client";

import { useEffect, useRef, useState } from "react";
import { apiFetch } from "@/lib/api-browser";
import { actorIdentity, type CurrentActor } from "@/lib/auth/actor";
import { parseMyr } from "@/lib/currency-input";
import type { ListingDraft } from "@/lib/vehicles/listing-draft";
import {
  matchesListingEstimate,
  parseListingEstimate,
  type ListingEstimate,
} from "@/lib/vehicles/listing-estimate";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";

interface EstimateRead {
  key: string;
  estimate: ListingEstimate | null;
  phase: "waiting" | "loading" | "ready" | "failed";
}

export function useListingEstimate(
  snapshot: ListingWorkspace,
  actor: CurrentActor,
  draft: ListingDraft,
  active: boolean,
) {
  const { vehicleId, branchId } = snapshot.vehicle;
  const identity = actorIdentity(actor);
  const actorId = actor.userId;
  const vehiclePrice = parseMyr(draft.vehiclePrice);
  const roadTax = parseMyr(draft.roadTax);
  const valid = vehiclePrice !== null && vehiclePrice > 0 && roadTax !== null;
  const key = JSON.stringify([identity, vehicleId, branchId, draft.vehiclePrice, draft.roadTax]);
  const [entry, setEntry] = useState<EstimateRead>({ key, estimate: null, phase: "waiting" });
  const [attempt, setAttempt] = useState(0);
  const controller = useRef<AbortController | null>(null);
  if (entry.key !== key) {
    setEntry({ key, estimate: null, phase: "waiting" });
  }

  useEffect(() => {
    if (!active || vehiclePrice === null || vehiclePrice <= 0 || roadTax === null) {
      return;
    }
    const cancellation = new AbortController();
    controller.current = cancellation;
    const expected = { actorId, vehicleId, branchId, vehiclePrice, roadTax };
    async function load() {
      setEntry((current) => (current.key === key ? { ...current, phase: "loading" } : current));
      try {
        const result = await apiFetch(
          `/api/staff/listings/${vehicleId}/price-estimate`,
          parseListingEstimate,
          {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ expectedActor: identity, branchId, vehiclePrice, roadTax }),
            signal: cancellation.signal,
          },
          { requireActor: true, maximumResponseBytes: 8192 },
        );
        if (cancellation.signal.aborted) {
          return;
        }
        if (
          result.status !== 200 ||
          !result.data ||
          !matchesListingEstimate(result.data, expected)
        ) {
          throw new Error("Draft estimate unavailable");
        }
        setEntry((current) =>
          current.key === key ? { key, estimate: result.data, phase: "ready" } : current,
        );
      } catch {
        if (!cancellation.signal.aborted) {
          setEntry((current) => (current.key === key ? { ...current, phase: "failed" } : current));
        }
      }
    }
    const timer = window.setTimeout(() => void load(), 400);
    return () => {
      window.clearTimeout(timer);
      cancellation.abort();
      if (controller.current === cancellation) {
        controller.current = null;
      }
    };
  }, [
    snapshot,
    actorId,
    identity,
    vehicleId,
    branchId,
    vehiclePrice,
    roadTax,
    key,
    active,
    attempt,
  ]);

  function retry() {
    controller.current?.abort();
    setEntry((current) => ({ ...current, phase: "waiting" }));
    setAttempt((current) => current + 1);
  }

  const current = entry.key === key && valid;
  return {
    estimate: current ? entry.estimate : null,
    incomplete: !valid,
    busy: active && valid && (!current || entry.phase === "waiting" || entry.phase === "loading"),
    failed: current && entry.phase === "failed",
    retry,
  };
}
