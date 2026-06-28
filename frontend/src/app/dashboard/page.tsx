import { StaffActorBoundary } from "@/components/staff/StaffActorBoundary";
import { redirect } from "next/navigation";
import { staffActor } from "@/lib/staff/server";
import { actorDestination } from "@/lib/auth/actor";
import { StaffNotice } from "@/components/staff/StaffPage";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import { StaffFrame } from "@/components/staff/StaffFrame";
import { StaffLink } from "@/components/staff/StaffLink";
import { LoadingProgressReady } from "@/components/ui/LoadingProgress";
import styles from "@/components/staff/Staff.module.css";

export default async function DashboardPage() {
  const actor = await staffActor("/dashboard");
  if (!actor) {
    return (
      <>
        <LoadingProgressReady />
        <SessionCheckUnavailable />
      </>
    );
  }
  const destination = actorDestination(actor);
  if (destination !== "/dashboard") {
    redirect(destination);
  }
  return (
    <StaffFrame actor={actor}>
      <StaffActorBoundary
        actor={actor}
        path="/dashboard"
        parameters={{}}
        snapshotId={crypto.randomUUID()}
      >
        <LoadingProgressReady />
        <StaffNotice title="Administration">
          <p className={styles.noticeDescription}>
            Manage staff accounts, dealership settings, promotions and warranty packages.
          </p>
          <nav className={styles.adminTools} aria-label="Administration tools">
            <StaffLink href="/dashboard/accounts" prefetch={false}>
              <strong>Staff accounts</strong>
              <span>Invitations, active staff and deactivated accounts</span>
            </StaffLink>
            <StaffLink href="/dashboard/settings" prefetch={false}>
              <strong>Settings</strong>
              <span>Purchase fees, tax, discount limits and reservation duration</span>
            </StaffLink>
            <StaffLink href="/dashboard/settings?section=promos" prefetch={false}>
              <strong>Promos</strong>
              <span>Promotion codes, discounts and availability</span>
            </StaffLink>
            <StaffLink href="/dashboard/settings?section=warranties" prefetch={false}>
              <strong>Warranty packages</strong>
              <span>Package terms, coverage and prices</span>
            </StaffLink>
          </nav>
        </StaffNotice>
      </StaffActorBoundary>
    </StaffFrame>
  );
}
