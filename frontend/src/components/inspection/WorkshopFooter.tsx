import type { useInspectionEditor } from "@/hooks/use-inspection-editor";
import type { WorkshopSection } from "@/lib/inspection/query";
import { InspectionActions } from "./InspectionActions";
import workshop from "./Workshop.module.css";

type Editor = ReturnType<typeof useInspectionEditor>;

export function WorkshopFooter({
  editor,
  section,
  historical,
  repairEditing,
  onConfirm,
  onActionsContainer,
}: {
  editor: Editor;
  section: WorkshopSection;
  historical: boolean;
  repairEditing: boolean;
  onConfirm: (value: "sign" | "discard") => void;
  onActionsContainer: (container: HTMLDivElement | null) => void;
}) {
  const pendingInspection = editor.checklistDirty || editor.save.uncertain || editor.sign.uncertain;
  const context = footerContext(editor, section, historical, repairEditing);
  return (
    <footer className={workshop.footer}>
      <div className={workshop.footerContext}>
        <strong>{context.title}</strong>
        <span>{context.description}</span>
      </div>
      <div className={workshop.footerActions}>
        {(section === "inspection" || pendingInspection) && (
          <InspectionActions editor={editor} onConfirm={onConfirm} />
        )}
        <div ref={onActionsContainer} className={workshop.footerActions} />
      </div>
    </footer>
  );
}

function footerContext(
  editor: Editor,
  section: WorkshopSection,
  historical: boolean,
  repairEditing: boolean,
) {
  if (editor.save.uncertain || editor.sign.uncertain) {
    return {
      title: "Inspection action unconfirmed",
      description: "Reconcile the captured inspection action before continuing.",
    };
  }
  if (editor.checklistDirty) {
    return {
      title: "Unsaved inspection changes",
      description:
        "Your findings are retained across tabs. Save or discard them before leaving this job.",
    };
  }
  if (section === "inspection") {
    return {
      title: editor.editable ? "Inspection draft" : "Inspection review",
      description: editor.editable
        ? "Save your findings before signing this revision."
        : "Read-only findings. Actions depend on the current assignment.",
    };
  }
  if (section === "history" || historical) {
    return {
      title: section === "history" ? "Record history" : "Historical repair work",
      description: "Retained inspection and repair records are read only.",
    };
  }
  if (repairEditing) {
    return {
      title: "Edit repair work and budget",
      description:
        "Submit the required work and budget for approval. Your edits are retained across tabs.",
    };
  }
  return {
    title: "Repair work",
    description: "Review the requested work and budget before approving or completing repairs.",
  };
}
