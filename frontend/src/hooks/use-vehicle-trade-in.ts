"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import type { Quote, Taxonomy, VehicleInput } from "@/lib/intake/contracts";
import { parseTaxonomy } from "@/lib/intake/parse";
import { emptyValuation, type ValuationDraft } from "@/lib/intake/vehicle-draft";
import { parseTradeInQuote, tradeInRequest } from "@/lib/vehicles/trade-in";

const LAST_VALUATION_RETAINED = "Your last accepted valuation is still shown. Retry deliberately.";

export function useVehicleTradeIn(initialTaxonomy?: Taxonomy | null) {
  const hasInitial = initialTaxonomy !== undefined;
  const [taxonomy, setTaxonomy] = useState<Taxonomy | null>(initialTaxonomy ?? null);
  const [draft, setDraft] = useState<ValuationDraft>(emptyValuation);
  const [accepted, setAccepted] = useState<{ vehicle: VehicleInput; quote: Quote } | null>(null);
  const [busy, setBusy] = useState(!hasInitial);
  const [editing, setEditing] = useState(true);
  const [optionsFailed, setOptionsFailed] = useState(hasInitial && initialTaxonomy === null);
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

  function startNewValuation() {
    cancel();
    setEditing(true);
  }

  const readOptions = useCallback(() => {
    if (taxonomy || controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    return apiFetch("/api/intake/taxonomy", parseTaxonomy, { signal: attempt.signal })
      .then((result) => {
        if (attempt.signal.aborted || controller.current !== attempt) {
          return;
        }
        if (!result.data || result.data.currency !== "MYR") {
          throw new Error("Trade-in options unavailable");
        }
        setTaxonomy(result.data);
      })
      .catch(() => {
        if (!attempt.signal.aborted && controller.current === attempt) {
          setOptionsFailed(true);
          error("Trade-in options could not be loaded. Retry deliberately.");
        }
      })
      .finally(() => {
        if (!attempt.signal.aborted && controller.current === attempt) {
          setBusy(false);
          controller.current = null;
        }
      });
  }, [taxonomy, error]);

  useEffect(() => {
    if (!hasInitial) {
      void readOptions();
    }
  }, [hasInitial, readOptions]);

  function retryOptions() {
    if (taxonomy || controller.current) {
      return;
    }
    setOptionsFailed(false);
    setBusy(true);
    void readOptions();
  }

  async function valueVehicle() {
    if (!taxonomy || controller.current) {
      return;
    }
    const vehicle = tradeInRequest(draft);
    if (!vehicle) {
      error(
        `Enter a whole-number manufacture year and mileage using digits, then retry. ${LAST_VALUATION_RETAINED}`,
      );
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await apiFetch("/api/intake/valuations", parseTradeInQuote, {
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
            ? `Check the supported vehicle, year and whole-number mileage, then retry. ${LAST_VALUATION_RETAINED}`
            : `The trade-in could not be valued. ${LAST_VALUATION_RETAINED}`,
        );
        return;
      }
      setAccepted({ vehicle, quote: result.data });
      setEditing(false);
    } catch {
      if (!attempt.signal.aborted && controller.current === attempt) {
        error(`The trade-in could not be valued. ${LAST_VALUATION_RETAINED}`);
      }
    } finally {
      if (!attempt.signal.aborted && controller.current === attempt) {
        setBusy(false);
        controller.current = null;
      }
    }
  }

  return {
    taxonomy,
    draft,
    changeDraft,
    accepted,
    busy,
    editing,
    optionsFailed,
    retryOptions,
    valueVehicle,
    startNewValuation,
  };
}
