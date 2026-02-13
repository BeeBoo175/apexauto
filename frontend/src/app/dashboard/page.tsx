import { StaffActorBoundary } from "@/components/staff/StaffActorBoundary";
import { redirect } from "next/navigation";
import { staffActor } from "@/lib/staff/server";
import { hasIntakeAccess } from "@/lib/auth/actor";
import { StaffNotice } from "@/components/staff/StaffPage";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";

export default async function DashboardPage() {
  const actor = await staffActor("/dashboard");
  if (!actor) {
    return <SessionCheckUnavailable />;
  }
  if (hasIntakeAccess(actor)) {
    redirect("/dashboard/intake");
  }
  return (
    <StaffActorBoundary actor={actor} path="/dashboard" snapshotId={crypto.randomUUID()}>
      <StaffNotice title="Your staff workspace">
        <p className="text-muted">
          {actor.role === "WorkshopTechnician"
            ? "Inspection and workshop tools arrive in a later slice. Intake appointments are handled by sales and branch management."
            : "Administration tools arrive in a later slice. This role does not have intake access."}
        </p>
      </StaffNotice>
    </StaffActorBoundary>
  );
}
