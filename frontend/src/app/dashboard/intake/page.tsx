import { StaffFrame } from "@/components/staff/StaffFrame";
import { StaffNotice } from "@/components/staff/StaffPage";
import { IntakeWorklist } from "@/components/staff/IntakeWorklist";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import { actorIdentity, hasIntakeAccess } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import { staffActor } from "@/lib/staff/server";
import { privateIntakeRead, intakeRead } from "@/lib/intake/server";
import { parseBranch, parsePage } from "@/lib/intake/parse";
import { parseDetail, parseWorklistItem } from "@/lib/staff/parse";
import { staffParams, staffQuery } from "@/lib/staff/query";

export default async function IntakePage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const actor = await staffActor("/dashboard/intake");
  if (!actor) {
    return <SessionCheckUnavailable />;
  }
  if (!hasIntakeAccess(actor)) {
    return (
      <StaffFrame key={actorIdentity(actor)} actor={actor}>
        <StaffNotice title="Intake access unavailable">
          <p>This role cannot access intake appointments.</p>
          <a href="/dashboard">Return to your staff workspace</a>
        </StaffNotice>
      </StaffFrame>
    );
  }
  const params = await searchParams;
  const { filters, page } = staffParams(params);
  const selectedId = isUuid(params.appointment) ? params.appointment : "";
  const [initial, branches, selected] = await Promise.all([
    privateIntakeRead(`/api/staff/intake/appointments?${staffQuery(filters, page)}`, (value) =>
      parsePage(value, parseWorklistItem),
    ),
    actor.role === "GeneralManager"
      ? intakeRead("/api/branches?page=1&pageSize=20", (value) => parsePage(value, parseBranch))
      : null,
    selectedId
      ? privateIntakeRead(`/api/staff/intake/appointments/${selectedId}`, parseDetail)
      : null,
  ]);
  return (
    <StaffFrame key={actorIdentity(actor)} actor={actor} fitViewport>
      <IntakeWorklist
        actor={actor}
        initial={initial}
        filters={filters}
        page={page}
        branches={branches?.data ?? null}
        selectedId={selectedId}
        selected={selected}
      />
    </StaffFrame>
  );
}
