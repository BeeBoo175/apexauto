"use client";

import { useInspectionEditor } from "@/hooks/use-inspection-editor";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Page } from "@/lib/intake/contracts";
import type { Technician } from "@/lib/inspection/contracts";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import { StaffNotice } from "@/components/staff/StaffPage";
import { RefreshButton } from "@/components/ui/RefreshButton";
export function InspectionEditor({
  id,
  actor,
  initial,
}: {
  id: string;
  actor: CurrentActor;
  initial: ApiResult<EditorSnapshot>;
  technicians: Page<Technician> | null;
}) {
  const editor = useInspectionEditor(id, actor, initial);
  return (
    <StaffNotice title="Inspection">
      <RefreshButton
        label="Refresh inspection"
        busy={editor.refreshing}
        disabled={!editor.ready}
        onClick={() => void editor.refresh()}
      />
      {editor.snapshot ? (
        <>
          <p>
            {editor.snapshot.draft.vehicle.registrationPlate} · Revision{" "}
            {editor.snapshot.draft.revision}
          </p>
          <ul>
            {editor.snapshot.draft.catalog.map((point) => (
              <li key={point.code}>
                {point.code} · {point.description}
              </li>
            ))}
          </ul>
        </>
      ) : (
        <p>Inspection unavailable. Use refresh to retry.</p>
      )}
    </StaffNotice>
  );
}
