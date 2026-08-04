"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { actorIdentity } from "@/lib/auth/actor";
import { privateApiFetch } from "@/lib/api-browser";
import {
  parseStaffAccountPage,
  staffAccountQuery,
  type StaffAccountFilters,
  type StaffAccountRow,
  type StaffAccountPage,
} from "@/lib/accounts/staff-accounts";
import type { AddressMode } from "./use-staff-address";

export function useStaffAccountsList(
  initial: StaffAccountPage,
  filters: StaffAccountFilters,
  page: number,
  onLocation: (
    filters: StaffAccountFilters,
    page: number,
    mode: AddressMode,
    items: StaffAccountRow[],
  ) => void,
) {
  const [snapshot, setSnapshot] = useState(initial);
  const [active, setActive] = useState({ filters, page });
  const [draft, setDraft] = useState(filters);
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  useEffect(() => () => controller.current?.abort(), []);

  // An applied change pushes its address; a reload or a traversal passes its own mode.
  async function load(
    next = active.filters,
    nextPage = 1,
    supersede = false,
    mode: AddressMode = "push",
  ) {
    if (controller.current && !supersede) {
      return false;
    }
    controller.current?.abort();
    const attempt = new AbortController();
    const submitted = draft;
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await privateApiFetch(
        `/api/staff-accounts?${staffAccountQuery(next, nextPage)}`,
        parseStaffAccountPage,
        { signal: attempt.signal },
      );
      if (attempt.signal.aborted) {
        return false;
      }
      const confirmed = result.data;
      if (
        result.status !== 200 ||
        !confirmed ||
        confirmed.page !== nextPage ||
        confirmed.pageSize !== next.pageSize ||
        actorIdentity(confirmed.actor) !== actorIdentity(initial.actor)
      ) {
        throw new Error("Staff accounts unavailable");
      }
      setSnapshot(confirmed);
      setActive({ filters: next, page: nextPage });
      setDraft((current) => (current === submitted ? next : current));
      setRevision((current) => current + 1);
      onLocation(next, nextPage, mode, confirmed.items);
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "Staff accounts could not be refreshed. Confirmed rows, filters and drafts remain shown.",
        );
      }
      return false;
    } finally {
      if (controller.current === attempt) {
        controller.current = null;
        setBusy(false);
      }
    }
  }

  function cancel() {
    controller.current?.abort();
    controller.current = null;
    setBusy(false);
  }

  return { cancel, snapshot, active, draft, setDraft, busy, revision, load };
}
