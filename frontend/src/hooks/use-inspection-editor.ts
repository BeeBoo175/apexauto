"use client";

import { useEffect, useRef, useState } from "react";
import { useInspectionLock } from "./use-inspection-lock";
import { useInitialToast } from "./use-initial-toast";
import { useToast } from "@/components/ui/Toast";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseEditor, type EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import { checklistDraft, type ChecklistDraft } from "@/lib/inspection/draft";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ApiResult } from "@/lib/intake/contracts";

export function useInspectionEditor(
  id: string,
  actor: CurrentActor,
  initial: ApiResult<EditorSnapshot>,
  onChanged?: () => void,
) {
  const [snapshot, setSnapshot] = useState(initial.data);
  const [base, setBase] = useState(initial.data?.draft ?? null);
  const [value, setValue] = useState<ChecklistDraft>(() =>
    initial.data
      ? checklistDraft(initial.data.draft, initial.data.photos)
      : { entries: {}, examined: [], photos: {} },
  );
  const [dirty, setDirty] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [childLocked, setChildLocked] = useState(false);
  const [ready, setReady] = useState(false);
  const [entry, setEntry] = useState(initial);
  const [pendingSnapshot, setPendingSnapshot] = useState<EditorSnapshot | null>(null);
  const [formDrafts, setFormDrafts] = useState<Record<string, boolean>>({});
  const formDraftsRef = useRef<Record<string, boolean>>({});
  const [draftEpoch, setDraftEpoch] = useState(0);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  useInitialToast(initial.message);
  const operating = childLocked;
  const formsDirty = Object.values(formDrafts).some(Boolean);
  const lockNavigation = useInspectionLock(dirty || formsDirty || operating);
  if (entry !== initial) {
    setEntry(initial);
    if (initial.data) {
      if (formsDirty || operating) {
        setPendingSnapshot(initial.data);
      } else {
        setSnapshot(initial.data);
      }
      if (!dirty && !formsDirty && !operating) {
        setBase(initial.data.draft);
        setValue(checklistDraft(initial.data.draft, initial.data.photos));
      }
    }
  }
  useEffect(() => {
    queueMicrotask(() => setReady(true));
    return () => controller.current?.abort();
  }, []);

  async function refresh(afterCommand = false) {
    if (controller.current || (operating && !afterCommand)) {
      return false;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setRefreshing(true);
    try {
      const result = await inspectionFetch(`drafts/${id}/workspace`, parseEditor, {
        signal: attempt.signal,
      });
      if (attempt.signal.aborted) {
        return false;
      }
      if (result.status !== 200 || result.data?.draft.id !== id) {
        throw new Error("Editor unavailable");
      }
      // Defer refreshed versions while child forms still hold edits against the current snapshot.
      if (Object.values(formDraftsRef.current).some(Boolean)) {
        setPendingSnapshot(result.data);
        return true;
      }
      setSnapshot(result.data);
      setPendingSnapshot(null);
      setConfirmed(false);
      if (!dirty || afterCommand) {
        setBase(result.data.draft);
        setValue(checklistDraft(result.data.draft, result.data.photos));
        setDirty(false);
      }
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        toast.error(
          "The inspection could not be refreshed. Confirmed details, your draft and captured operations remain shown.",
        );
      }
      return false;
    } finally {
      if (!attempt.signal.aborted) {
        setRefreshing(false);
      }
      controller.current = null;
    }
  }

  function change(next: ChecklistDraft) {
    lockNavigation(true);
    setDirty(true);
    setValue(next);
  }

  function reset() {
    const current = pendingSnapshot ?? snapshot;
    if (!current || operating) {
      return;
    }
    setSnapshot(current);
    setPendingSnapshot(null);
    setValue(checklistDraft(current.draft, current.photos));
    setBase(current.draft);
    setDirty(false);
    formDraftsRef.current = {};
    setFormDrafts({});
    setDraftEpoch((value) => value + 1);
    lockNavigation(false);
  }

  function lockChild(locked: boolean) {
    lockNavigation(locked || dirty || Object.values(formDraftsRef.current).some(Boolean));
    setChildLocked(locked);
  }
  function markForm(name: string, dirty: boolean) {
    const next = { ...formDraftsRef.current, [name]: dirty };
    formDraftsRef.current = next;
    setFormDrafts(next);
    lockNavigation(dirty || Object.values(next).some(Boolean) || operating);
  }
  async function actionConfirmed() {
    onChanged?.();
    setConfirmed(true);
    await refresh(true);
  }
  const stale = Boolean(
    base &&
    snapshot &&
    (base.assignmentVersion !== snapshot.draft.assignmentVersion ||
      base.revision !== snapshot.draft.revision ||
      base.latestSave?.version !== snapshot.draft.latestSave?.version),
  );
  const editable = Boolean(
    snapshot &&
    base &&
    actor.role === "WorkshopTechnician" &&
    snapshot.draft.technicianId === actor.userId &&
    !snapshot.draft.isSigned &&
    !confirmed &&
    !stale,
  );
  return {
    snapshot,
    base,
    value,
    change,
    dirty: dirty || formsDirty,
    reset,
    refreshing,
    refresh,
    operating,
    childLocked,
    lockChild,
    ready,
    confirmed,
    stale,
    editable,
    actionConfirmed,
    markForm,
    draftEpoch,
    pendingSnapshot,
  };
}
