"use client";

import { useState } from "react";
import { parseMyr } from "@/lib/currency-input";
import { calculateFinancing, defaultCashContribution } from "@/lib/vehicles/financing";

export function useVehicleFinancing(total: number) {
  const [cashDraft, setCashDraft] = useState<string | null>(null);
  const [termYears, setTermYears] = useState("7");
  const cashContribution = cashDraft ?? defaultCashContribution(total);
  const cash = parseMyr(cashContribution);
  const estimate = cash === null ? null : calculateFinancing(total, cash, Number(termYears));
  const error =
    cash === null
      ? "Enter a non-negative cash contribution in MYR with at most two decimal places."
      : null;

  return {
    cashContribution,
    setCashContribution: setCashDraft,
    termYears,
    setTermYears,
    estimate,
    error,
  };
}
