"use client";
import { useEffect, useRef, useState } from "react";
import { useIntakeCommand } from "./use-intake-command";
import { useInspectionLock } from "./use-inspection-lock";
import { useInitialToast } from "./use-initial-toast";
import { useToast } from "@/components/ui/Toast";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseSave } from "@/lib/inspection/parse";
import { parseReport } from "@/lib/inspection/parse-report";
import { parseEditor, type EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import { checklistDraft, saveContents, type ChecklistDraft } from "@/lib/inspection/draft";
import { isRecord } from "@/lib/auth/contracts";
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
  const save = useIntakeCommand(
    `drafts/${id}/saves`,
    parseSave,
    (r, body) =>
      isRecord(body) &&
      r.operationId === body.operationId &&
      r.inspectionId === id &&
      r.actorId === actor.userId &&
      r.assignmentVersion === body.assignmentVersion &&
      r.expectedVersion === body.expectedVersion,
    inspectionFetch,
  );
  const sign = useIntakeCommand(
    `drafts/${id}/sign`,
    parseReport,
    (r, body) =>
      isRecord(body) &&
      r.operationId === body.operationId &&
      r.inspectionId === id &&
      r.technicianId === actor.userId &&
      r.assignmentVersion === body.assignmentVersion &&
      r.revision === body.revision &&
      r.saveVersion === body.saveVersion,
    inspectionFetch,
  );
  const operating = save.busy || save.uncertain || sign.busy || sign.uncertain || childLocked;
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

  async function saveDraft() {
    if (!base || (operating && !save.uncertain)) {
      return;
    }
    const contents = saveContents(base, value);
    if (!contents.body) {
      toast.error(contents.error);
      return;
    }
    lockNavigation(true);
    const result = await save.submit({ operationId: crypto.randomUUID(), ...contents.body });
    if (result?.status === 200 && result.data) {
      onChanged?.();
      const next = { ...base, latestSave: result.data };
      setBase(next);
      setSnapshot((s) => (s ? { ...s, draft: next } : s));
      setDirty(false);
      lockNavigation(false);
      setConfirmed(true);
      await refresh(true);
    }
  }

  async function signDraft() {
    if (!base || dirty || (operating && !sign.uncertain)) {
      return;
    }
    lockNavigation(true);
    const result = await sign.submit({
      operationId: crypto.randomUUID(),
      assignmentVersion: base.assignmentVersion,
      saveVersion: base.latestSave?.version ?? 0,
      revision: base.revision,
    });
    if (result?.status === 200 && result.data) {
      onChanged?.();
      setConfirmed(true);
      lockNavigation(false);
      await refresh(true);
    }
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
    save,
    sign,
    saveDraft,
    signDraft,
    actionConfirmed,
    markForm,
    draftEpoch,
    pendingSnapshot,
  };
}
