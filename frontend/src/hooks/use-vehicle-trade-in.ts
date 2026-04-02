"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import type { Quote, Taxonomy, VehicleInput } from "@/lib/intake/contracts";
import { parseTaxonomy } from "@/lib/intake/parse";
import { emptyValuation, type ValuationDraft } from "@/lib/intake/vehicle-draft";
import { parseTradeInQuote, tradeInRequest } from "@/lib/vehicles/trade-in";

export function useVehicleTradeIn() {
  const [taxonomy, setTaxonomy] = useState<Taxonomy | null>(null);
  const [draft, setDraft] = useState<ValuationDraft>(emptyValuation);
  const [accepted, setAccepted] = useState<{ vehicle: VehicleInput; quote: Quote } | null>(null);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { error } = useToast();

  useEffect(() => {
    return () => {
      controller.current?.abort();
      controller.current = null;
    };
  }, []);

  function cancel() {
    controller.current?.abort();
    controller.current = null;
    setBusy(false);
  }

  function changeDraft(value: ValuationDraft) {
    cancel();
    setDraft(value);
  }

  function remove() {
    cancel();
    setAccepted(null);
  }

  async function loadOptions() {
    if (taxonomy || controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await apiFetch("/api/intake/taxonomy", parseTaxonomy, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      if (!result.data || result.data.currency !== "MYR") {
        throw new Error("Trade-in options unavailable");
      }
      setTaxonomy(result.data);
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        error("Trade-in options could not be loaded. Retry deliberately.");
      }
    } finally {
      if (!attempt.signal.aborted && controller.current === attempt) {
        setBusy(false);
        controller.current = null;
      }
    }
  }

  async function valueVehicle() {
    if (!taxonomy || controller.current) {
      return;
    }
    const vehicle = tradeInRequest(draft);
    if (!vehicle) {
      error(
        "Enter a whole-number manufacture year and mileage using digits, then retry. Your last accepted valuation is still shown.",
      );
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await apiFetch("/api/intake/valuation", parseTradeInQuote, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(vehicle),
        signal: attempt.signal,
      });
      if (attempt.signal.aborted || controller.current !== attempt) {
        return;
      }
      if (!result.data) {
        error(
          result.status === 400
            ? "Check the supported vehicle, year and whole-number mileage, then retry. Your last accepted valuation is still shown."
            : "The trade-in could not be valued. Your last accepted valuation is still shown. Retry deliberately.",
        );
        return;
      }
      setAccepted({ vehicle, quote: result.data });
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        error(
          "The trade-in could not be valued. Your last accepted valuation is still shown. Retry deliberately.",
        );
      }
    } finally {
      if (!attempt.signal.aborted && controller.current === attempt) {
        setBusy(false);
        controller.current = null;
      }
    }
  }

  return { taxonomy, draft, changeDraft, accepted, busy, loadOptions, valueVehicle, remove };
}
