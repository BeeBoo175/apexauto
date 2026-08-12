import { parseMyr } from "@/lib/currency-input";
import { severities, type Draft, type Entry, type Point } from "./contracts";

export type EntryDraft = Omit<Entry, "estimatedRepairCost"> & { estimate: string };
export type ChecklistDraft = {
  entries: Record<string, EntryDraft>;
  examined: string[];
  photos: Record<string, string[]>;
};

export type ChecklistView = "all" | "findings" | "remaining";

export function checklistViews(catalog: Point[], value: ChecklistDraft, search: string) {
  const query = search.toLowerCase();
  const all = catalog.filter((point) =>
    `${point.code} ${point.description} ${point.category}`.toLowerCase().includes(query),
  );
  const examined = new Set(value.examined);
  return {
    all,
    findings: all.filter((point) => value.entries[point.code]?.kind === "Finding"),
    // Points recorded as not fitted are settled without examination, so they never count as remaining.
    remaining: all.filter(
      (point) => value.entries[point.code]?.kind !== "NotFitted" && !examined.has(point.code),
    ),
  };
}

export function checklistDraft(
  draft: Draft,
  photos: { id: string; pointCode: string }[],
): ChecklistDraft {
  const entries = Object.fromEntries(
    (draft.latestSave?.entries ?? []).map((entry) => [
      entry.pointCode,
      { ...entry, estimate: entry.estimatedRepairCost.toFixed(2) },
    ]),
  );
  const grouped: Record<string, string[]> = {};
  for (const photo of photos) {
    grouped[photo.pointCode] = [...(grouped[photo.pointCode] ?? []), photo.id];
  }
  const save = draft.latestSave;
  let examined: string[] = [];
  // Findings survive handover and reopening; the examination declaration must be renewed.
  if (
    save &&
    save.assignmentVersion === draft.assignmentVersion &&
    save.version > draft.reopenedAfterSaveVersion
  ) {
    examined = save.examinedPointCodes ?? [];
  }
  return { entries, examined, photos: grouped };
}

export function saveContents(draft: Draft, value: ChecklistDraft) {
  const entries = Object.values(value.entries);
  const invalid = entries.find(
    (entry) =>
      !entry.notes.trim() ||
      /[\u0000-\u001f\u007f]/.test(entry.notes) ||
      (entry.kind === "Finding" && parseMyr(entry.estimate) === null),
  );
  if (invalid) {
    return {
      error: `${invalid.pointCode}: enter notes without line breaks and an exact nonnegative MYR estimate.`,
      body: null,
    };
  }
  const fitted = draft.catalog
    .filter((point) => value.entries[point.code]?.kind !== "NotFitted")
    .map((point) => point.code);
  const examined = new Set(value.examined);
  const attested = fitted.every((code) => examined.has(code));
  return {
    error: "",
    body: {
      assignmentVersion: draft.assignmentVersion,
      expectedVersion: draft.latestSave?.version ?? 0,
      entries: entries.map((entry) => ({
        pointCode: entry.pointCode,
        kind: entry.kind === "Finding" ? 0 : 1,
        severity: entry.kind === "Finding" ? severities.indexOf(entry.severity!) : null,
        estimatedRepairCost: entry.kind === "Finding" ? parseMyr(entry.estimate) : 0,
        notes: entry.notes.trim(),
      })),
      photoIds: Object.entries(value.photos)
        .filter(([code]) => value.entries[code]?.kind === "Finding")
        .flatMap(([, ids]) => ids),
      examinedPointCodes: attested ? fitted : null,
    },
  };
}
