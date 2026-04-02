"use client";

import { useLayoutEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import {
  matchesPurchaseTerms,
  parsePurchaseQuote,
  promoCode,
  purchaseTermsKey,
  type PurchaseQuote,
} from "@/lib/vehicles/purchase-quote";

export function useVehiclePromo(detail: PublicVehicleDetail | null) {
  const terms = detail ? purchaseTermsKey(detail) : "";
  const [entry, setEntry] = useState(terms);
  const [draft, setDraft] = useState("");
  const [accepted, setAccepted] = useState<PurchaseQuote | null>(null);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();

  if (entry !== terms) {
    setEntry(terms);
    setAccepted(null);
    setBusy(false);
  }

  useLayoutEffect(() => {
    return () => {
      controller.current?.abort();
      controller.current = null;
    };
  }, [terms]);

  function changeCode(value: string) {
    controller.current?.abort();
    controller.current = null;
    setBusy(false);
    setDraft(value);
  }

  function remove() {
    changeCode("");
    setAccepted(null);
  }

  async function apply() {
    const code = promoCode(draft);
    if (!detail || !code || controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await apiFetch(
        "/api/promos/validate",
        (value) => parsePurchaseQuote(value, code),
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ vehicleId: detail.listing.id, code }),
          signal: attempt.signal,
        },
      );
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      if (!result.data) {
        error(
          result.status === 400
            ? "This promo cannot apply to this vehicle. Check its exact case and try again."
            : "The promo could not be checked. Your last confirmed estimate is still shown. Retry deliberately.",
        );
        return;
      }
      if (!matchesPurchaseTerms(result.data, detail.price)) {
        error("The vehicle's pricing has changed. Refresh the vehicle before applying a promo.");
        return;
      }
      setAccepted(result.data);
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        error(
          "The promo could not be checked. Your last confirmed estimate is still shown. Retry deliberately.",
        );
      }
    } finally {
      if (!attempt.signal.aborted && controller.current === attempt) {
        setBusy(false);
        controller.current = null;
      }
    }
  }

  return { draft, changeCode, accepted, busy, apply, remove };
}
