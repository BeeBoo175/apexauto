import { StaffFrame } from "@/components/staff/StaffFrame";
import { actorIdentity } from "@/lib/auth/actor";
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
      <StaffFrame key={actorIdentity(actor)} actor={actor}>
        <StaffNotice title="Intake access unavailable">
          <a href="/dashboard">Return to your staff workspace</a>
        </StaffNotice>
      </StaffFrame>
    );
  }
  const initial = await privateIntakeRead(`/api/staff/intake/appointments/${id}`, parseDetail);
  if (initial.status === 404) {
    notFound();
  }
  const navigation = await searchParams;
  const { filters, page } = staffParams(navigation);
  const returnTo = `/dashboard/intake?${staffViewQuery(filters, page)}`;
  return (
    <StaffFrame key={actorIdentity(actor)} actor={actor}>
      <StaffCommandBar title="Intake appointment" />
      <div className={styles.direct}>
        <a className={styles.backLink} href={returnTo}>
          Back to intake appointments
        </a>
        <Card>
          <AppointmentWorkbench key={id} id={id} actor={actor} initial={initial} />
        </Card>
      </div>
    </StaffFrame>
  );
}
