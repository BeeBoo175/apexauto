"use client";
import { useEffect, useMemo, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import { defaultIntakeDateWindow, validateIntakeDate } from "@/lib/intake/appointment-date";
import { parseAppointmentPrefill } from "@/lib/intake/appointment-prefill";
import { parseSlots } from "@/lib/intake/parse";
import type { Branch } from "@/lib/branches";
import type { Page } from "@/lib/api";
import type { SlotOffer } from "@/lib/intake/contracts";
import { readBranchChoices } from "@/lib/branch-choices";

// `addressed` lets the seller address name a showroom and bay to preselect, as the showroom booking block links it.
export function useSellerAppointment(initialBranches: Page<Branch>, addressed = false) {
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
    await readSlots(branchId, date, "");
  }
  // `preselect` is a bay to keep chosen when the live offer still lists it.
  async function readSlots(wantedBranch: string, wantedDate: string, preselect: string) {
    controller.current?.abort();
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await apiFetch(
        `/api/intake/slots?branchId=${encodeURIComponent(wantedBranch)}&date=${encodeURIComponent(wantedDate)}`,
        parseSlots,
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (
        !result.data ||
        result.status !== 200 ||
        result.data.branchId !== wantedBranch ||
        result.data.date !== wantedDate
      ) {
        throw new Error(result.message);
      }
      const stillOffered = result.data.slots.some((slot) => slot.startsAt === preselect);
      setOffer(result.data);
      setStartsAt(stillOffered ? preselect : "");
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
  useEffect(() => {
    if (!addressed) {
      return;
    }
    const prefill = parseAppointmentPrefill(window.location.search);
    const known = initialBranches.items.find((branch) => branch.id === prefill?.branchId);
    if (!prefill || !known) {
      return;
    }
    queueMicrotask(() => {
      selectBranch(known.id);
      if (prefill.date && !validateIntakeDate(prefill.date, defaultIntakeDateWindow())) {
        setDate(prefill.date);
        void readSlots(known.id, prefill.date, prefill.startsAt ?? "");
      }
    });
    // Read once on arrival; later choices belong to the form.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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
