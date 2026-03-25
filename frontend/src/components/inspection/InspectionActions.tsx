import { Button } from "@/components/ui/Button";
import type { useInspectionEditor } from "@/hooks/use-inspection-editor";

export function InspectionActions({
  editor,
  onConfirm,
}: {
  editor: ReturnType<typeof useInspectionEditor>;
  onConfirm: (value: "sign" | "discard") => void;
}) {
  const { base } = editor;
  if (!base) {
    return null;
  }
  const disabled = !editor.ready || editor.operating || editor.refreshing || editor.confirmed;
  const attested = Boolean(base.latestSave?.examinedPointCodes);
  if (!editor.editable && !editor.sign.uncertain && !editor.checklistDirty) {
    return null;
  }
  return (
    <>
      {editor.editable && (
        <Button
          size="compact"
          disabled={
            editor.save.busy ||
            editor.sign.busy ||
            editor.childLocked ||
            editor.refreshing ||
            editor.sign.uncertain
          }
          onClick={() => void editor.saveDraft()}
        >
          {editor.save.uncertain ? "Reconcile draft save" : "Save draft"}
        </Button>
      )}
      {editor.editable && (
        <Button
          size="compact"
          variant="secondary"
          disabled={
            disabled ||
            editor.dirty ||
            !attested ||
            (base.latestSave?.version ?? 0) <= base.reopenedAfterSaveVersion
          }
          onClick={() => onConfirm("sign")}
        >
          Sign inspection
        </Button>
      )}
      {editor.sign.uncertain && (
        <Button size="compact" disabled={editor.sign.busy} onClick={() => void editor.signDraft()}>
          Reconcile sign-off
        </Button>
      )}
      {editor.checklistDirty && (
        <Button
          size="compact"
          variant="secondary"
          disabled={editor.operating}
          onClick={() => onConfirm("discard")}
        >
          Discard local changes
        </Button>
      )}
    </>
  );
}
