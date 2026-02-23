import { StaffActorBoundary } from "@/components/staff/StaffActorBoundary";
import { redirect } from "next/navigation";
import { staffActor } from "@/lib/staff/server";
import { hasIntakeAccess } from "@/lib/auth/actor";
import { StaffNotice } from "@/components/staff/StaffPage";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import styles from "@/components/staff/Staff.module.css";

export default async function DashboardPage() {
  const actor = await staffActor("/dashboard");
  if (!actor) {
    return <SessionCheckUnavailable />;
  }
  if (hasIntakeAccess(actor)) {
    redirect("/dashboard/intake");
  }
  if (actor.role === "WorkshopTechnician") {
    redirect("/dashboard/inspection");
  }
  return (
    <StaffActorBoundary actor={actor} path="/dashboard" snapshotId={crypto.randomUUID()}>
      <StaffNotice title="Your staff workspace">
        <p className={styles.noticeDescription}>
          Administration tools arrive in a later slice. This role does not have intake access.
        </p>
      </StaffNotice>
    </StaffActorBoundary>
  );
}
