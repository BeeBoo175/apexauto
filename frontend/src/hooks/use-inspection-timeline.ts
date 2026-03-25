"use client";

import { useEffect, useRef, useState } from "react";
import { useInspectionHistory } from "./use-inspection-history";
import { parseRevisions, parseRepairs } from "@/lib/inspection/parse-report";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";

export function useInspectionTimeline(snapshot: EditorSnapshot | null, active: boolean) {
  const id = snapshot?.draft.id ?? "";
  const revision = snapshot?.draft.revision ?? 0;
  const [selection, setSelection] = useState({ id, revision, previous: 0 });
  const previousSnapshot = useRef(snapshot);
  if (selection.id !== id || selection.revision !== revision) {
    setSelection({ id, revision, previous: 0 });
  }
  const previousRevision =
    selection.id === id && selection.revision === revision ? selection.previous : 0;
  const revisions = useInspectionHistory(
    snapshot?.revisions ?? null,
    `reports/${id}/revisions`,
    parseRevisions,
    "Revision history could not be refreshed. Revealed revisions remain shown.",
    { inspectionId: id },
    { enabled: active, progressive: true, attempted: snapshot?.historyRequested },
  );
  const repairs = useInspectionHistory(
    snapshot?.repairs ?? null,
    `reports/${id}/revisions/${revision}/repair-history`,
    parseRepairs,
    "Repair history could not be refreshed. Revealed receipts remain shown.",
    { inspectionId: id, revision },
    {
      enabled: active && Boolean(snapshot?.draft.isSigned),
      progressive: true,
      attempted: snapshot?.historyRequested && snapshot.draft.isSigned,
    },
  );
  const previous = useInspectionHistory(
    null,
    `reports/${id}/revisions/${previousRevision}/repair-history`,
    parseRepairs,
    "Previous repair work could not be loaded. Revealed work and your current draft remain shown.",
    { inspectionId: id, revision: previousRevision },
    { enabled: active && previousRevision > 0 && previousRevision !== revision, progressive: true },
  );

  useEffect(() => {
    const before = previousSnapshot.current;
    previousSnapshot.current = snapshot;
    if (!snapshot || before === snapshot || before?.draft.id !== id) {
      return;
    }
    if (!snapshot.historyRequested && revisions.attempted) {
      void revisions.refresh();
    }
    if (!snapshot.historyRequested && before.draft.revision === revision && repairs.attempted) {
      void repairs.refresh();
    }
    if (previousRevision > 0 && previousRevision !== revision && previous.attempted) {
      void previous.refresh();
    }
  }, [snapshot, id, revision, revisions, repairs, previous, previousRevision]);

  return {
    revisions,
    repairs,
    previous,
    previousRevision,
    selectRevision: (previous: number) => setSelection({ id, revision, previous }),
  };
}
