import { StaffActorBoundary } from "@/components/staff/StaffActorBoundary";
import { StaffLink } from "@/components/staff/StaffLink";
import { notFound } from "next/navigation";
import { isUuid } from "@/lib/auth/contracts";
import { hasIntakeAccess } from "@/lib/auth/actor";
import { staffActor } from "@/lib/staff/server";
import { privateIntakeRead } from "@/lib/intake/server";
import { parseDetail } from "@/lib/staff/parse";
import { StaffCommandBar, StaffNotice } from "@/components/staff/StaffPage";
import { AppointmentWorkbench } from "@/components/staff/AppointmentWorkbench";
import { Card } from "@/components/ui/Card";
import { SessionCheckUnavailable } from "@/components/auth/SessionCheckUnavailable";
import { staffParams, staffViewQuery } from "@/lib/staff/query";
import { calendarHref, calendarParams } from "@/lib/staff/calendar";
import styles from "@/components/staff/Staff.module.css";

export default async function IntakeDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { id } = await params;
  if (!isUuid(id)) {
    notFound();
  }
  const actor = await staffActor(`/dashboard/intake/${id}`);
  if (!actor) {
    return <SessionCheckUnavailable />;
  }
  if (!hasIntakeAccess(actor)) {
    return (
      <StaffActorBoundary
        actor={actor}
        path={`/dashboard/intake/${id}`}
        snapshotId={crypto.randomUUID()}
      >
        <StaffNotice title="Intake access unavailable">
          <a href="/dashboard">Return to your staff workspace</a>
        </StaffNotice>
      </StaffActorBoundary>
    );
  }
  const initial = await privateIntakeRead(`/api/staff/intake/appointments/${id}`, parseDetail);
  if (initial.status === 404) {
    notFound();
  }
  const navigation = await searchParams;
  const { filters, page } = staffParams(navigation);
  const returnTo =
    navigation.view === "list"
      ? `/dashboard/intake?${staffViewQuery(filters, page)}`
      : calendarHref(calendarParams(navigation));
  return (
    <StaffActorBoundary
      actor={actor}
      path={`/dashboard/intake/${id}`}
      snapshotId={crypto.randomUUID()}
    >
      <StaffCommandBar title="Intake appointment" />
      <div className={styles.direct}>
        <StaffLink className={styles.backLink} href={returnTo} prefetch={false}>
          Back to intake appointments
        </StaffLink>
        <Card>
          <AppointmentWorkbench key={id} id={id} actor={actor} initial={initial} />
        </Card>
      </div>
    </StaffActorBoundary>
  );
}
