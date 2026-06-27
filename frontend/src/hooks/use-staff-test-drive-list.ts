"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { actorIdentity } from "@/lib/auth/actor";
import { privateApiFetch } from "@/lib/api-browser";
import { parseStaffTestDrivePage } from "@/lib/test-drives/staff";
import {
  staffTestDriveQuery,
  staffTestDriveUrl,
  testDriveSectionParameter,
  testDriveDateError,
  type StaffTestDrivePage,
  type TestDriveFilters,
} from "@/lib/test-drives/staff-query";

export function useStaffTestDriveList(
  initial: StaffTestDrivePage,
  filters: TestDriveFilters,
  page: number,
) {
  const [snapshot, setSnapshot] = useState(initial);
  const [active, setActive] = useState({ filters, page });
  const [draft, setDraft] = useState(filters);
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  useEffect(() => () => controller.current?.abort(), []);

  async function load(next = active.filters, nextPage = 1, supersede = false, history = true) {
    if (controller.current && !supersede) {
      return false;
    }
    const invalid = testDriveDateError(next);
    if (invalid) {
      toast.error(invalid);
      return false;
    }
    controller.current?.abort();
    const attempt = new AbortController();
    const submitted = draft;
    controller.current = attempt;
    setBusy(true);
    try {
      const result = await privateApiFetch(
        `/api/staff/test-drives?${staffTestDriveQuery(next, nextPage)}`,
        parseStaffTestDrivePage,
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
        throw new Error("Test drives unavailable");
      }
      setSnapshot(confirmed);
      setActive({ filters: next, page: nextPage });
      setDraft((current) => (current === submitted ? next : current));
      setRevision((current) => current + 1);
      if (history) {
        const current = new URL(window.location.href).searchParams;
        const selected = current.get("booking") ?? "";
        const section = current.get(testDriveSectionParameter) ?? "";
        window.history.replaceState(null, "", staffTestDriveUrl(next, nextPage, selected, section));
      }
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "Test drives could not be refreshed. Confirmed rows, filters and drafts remain shown.",
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

  return { snapshot, active, draft, setDraft, busy, revision, load };
}
