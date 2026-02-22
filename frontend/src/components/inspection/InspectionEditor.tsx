import type { CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Page } from "@/lib/intake/contracts";
import type { Technician } from "@/lib/inspection/contracts";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import { StaffNotice } from "@/components/staff/StaffPage";
import { StaffLink } from "@/components/staff/StaffLink";
export function InspectionEditor({
  id,
  initial,
}: {
  id: string;
  actor: CurrentActor;
  initial: ApiResult<EditorSnapshot>;
  technicians: Page<Technician> | null;
}) {
  const snapshot = initial.data;
  if (!snapshot)
    return (
      <StaffNotice title="Inspection unavailable">
        <StaffLink prefetch={false} href={`/dashboard/inspection/${id}`}>
          Retry inspection
        </StaffLink>
      </StaffNotice>
    );
  return (
    <StaffNotice title="Inspection">
      <p>
        {snapshot.draft.vehicle.registrationPlate} · Revision {snapshot.draft.revision}
      </p>
      <p>
        {snapshot.draft.vehicle.branchName} · {snapshot.draft.vehicle.status}
      </p>
      <ul>
        {snapshot.draft.catalog.map((point) => (
          <li key={point.code}>
            {point.code} · {point.description}
          </li>
        ))}
      </ul>
    </StaffNotice>
  );
}
