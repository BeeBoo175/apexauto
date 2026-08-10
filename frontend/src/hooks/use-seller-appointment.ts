"use client";
import { useEffect, useMemo, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import { defaultIntakeDateWindow, validateIntakeDate } from "@/lib/intake/appointment-date";
import { parseSlots } from "@/lib/intake/parse";
import type { Branch } from "@/lib/branches";
import type { Page } from "@/lib/api";
import type { SlotOffer } from "@/lib/intake/contracts";
import { readBranchChoices } from "@/lib/branch-choices";

export function useSellerAppointment(initialBranches: Page<Branch>) {
  const [branches, setBranches] = useState(initialBranches);
  const [branchId, setBranchId] = useState("");
  const [selectedBranch, setSelectedBranch] = useState<Branch | null>(null);
  const [date, setDate] = useState("");
  const [startsAt, setStartsAt] = useState("");
  const [offer, setOffer] = useState<SlotOffer | null>(null);
  const [busy, setBusy] = useState(false);
  const toast = useToast();
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);

  const dateWindow = useMemo(
    () => offer?.dateWindow ?? defaultIntakeDateWindow(),
    [offer?.dateWindow],
  );
  const dateError = useMemo(() => validateIntakeDate(date, dateWindow), [date, dateWindow]);

  function selectBranch(id: string) {
    controller.current?.abort();
    setBusy(false);
    setBranchId(id);
    setSelectedBranch(
      id ? (branches.items.find((branch) => branch.id === id) ?? selectedBranch) : null,
    );
    setStartsAt("");
    setOffer(null);
  }
  function selectDate(value: string) {
    controller.current?.abort();
    setBusy(false);
    setDate(value);
    setStartsAt("");
    setOffer(null);
  }
  async function loadSlots() {
    if (!branchId || !date || validateIntakeDate(date, dateWindow)) {
      return;
    }
    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
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
      if (
        !result.data ||
        result.status !== 200 ||
        result.data.branchId !== branchId ||
        result.data.date !== date
      ) {
        throw new Error(result.message);
      }
      setOffer(result.data);
      setStartsAt("");
    } catch (error) {
      if (!attempt.signal.aborted) {
        toast.error(
          error instanceof Error && error.message
            ? error.message
            : "Slots could not be refreshed. The last confirmed offer remains visible.",
        );
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }
  async function loadBranches(page: number) {
    if (busy) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await readBranchChoices(page, attempt.signal, undefined, { fresh: true });
      if (attempt.signal.aborted) {
        return;
      }
      if (result.status !== 200 || !result.data) {
        throw new Error("Branches could not be refreshed. Existing choices remain visible.");
      }
      setBranches(result.data);
    } catch (error) {
      if (!attempt.signal.aborted) {
        toast.error(error instanceof Error ? error.message : "Branches are unavailable.");
      }
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }
  return {
    branches,
    selectedBranch,
    branchId,
    date,
    dateWindow,
    dateError,
    startsAt,
    setStartsAt,
    offer,
    busy,
    selectBranch,
    selectDate,
    loadSlots,
    loadBranches,
  };
}
