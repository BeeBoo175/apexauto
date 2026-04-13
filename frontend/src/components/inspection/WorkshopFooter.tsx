import type { useInspectionEditor } from "@/hooks/use-inspection-editor";
import type { WorkshopSection } from "@/lib/inspection/query";
import { InspectionActions } from "./InspectionActions";
import { Button } from "@/components/ui/Button";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import workspace from "@/components/staff/StaffWorkspace.module.css";

type Editor = ReturnType<typeof useInspectionEditor>;

export function WorkshopFooter({
  editor,
  section,
  historical,
  repairEditing,
  onConfirm,
  onActionsContainer,
  onCurrentRepairs,
}: {
  editor: Editor;
  section: WorkshopSection;
  historical: boolean;
  repairEditing: boolean;
  onConfirm: (value: "sign" | "discard") => void;
  onActionsContainer: (container: HTMLDivElement | null) => void;
  onCurrentRepairs: () => void;
}) {
  const pendingInspection = editor.checklistDirty || editor.save.uncertain || editor.sign.uncertain;
  const context = footerContext(editor, section, historical, repairEditing);
  return (
    <StaffWorkspaceFooter title={context.title} description={context.description}>
      {(section === "inspection" || pendingInspection) && (
        <InspectionActions editor={editor} onConfirm={onConfirm} />
      )}
      <div ref={onActionsContainer} className={workspace.footerActions} />
      {section === "repairs" && historical && (
        <Button
          size="compact"
          variant={pendingInspection ? "secondary" : "primary"}
          disabled={!editor.ready || editor.operating || editor.refreshing}
          onClick={onCurrentRepairs}
        >
          Back to current repairs
        </Button>
      )}
    </StaffWorkspaceFooter>
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
