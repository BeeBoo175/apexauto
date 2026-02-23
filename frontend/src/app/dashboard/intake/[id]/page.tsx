import { StaffEntry } from "@/components/staff/StaffEntry";
import { StaffLink } from "@/components/staff/StaffLink";
import { notFound } from "next/navigation";
import { isUuid } from "@/lib/auth/contracts";
import { hasIntakeAccess } from "@/lib/auth/actor";
import { privateIntakeRead } from "@/lib/intake/server";
import { parseDetail } from "@/lib/staff/parse";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { AppointmentWorkbench } from "@/components/staff/AppointmentWorkbench";
import { Card } from "@/components/ui/Card";
import type { SearchParams } from "@/lib/query";
import { staffParams, staffViewQuery } from "@/lib/staff/query";
import { calendarHref, calendarParams } from "@/lib/staff/calendar";
import styles from "@/components/staff/Staff.module.css";

export default async function IntakeDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SearchParams>;
}) {
  const { id } = await params;
  if (!isUuid(id)) {
    notFound();
  }
  return (
    <StaffEntry
      path={`/dashboard/intake/${id}`}
      access={hasIntakeAccess}
      deniedTitle="Intake access unavailable"
      deniedDescription="This role cannot access intake appointments."
    >
      {async (actor) => {
        const initial = await privateIntakeRead(
          `/api/staff/intake/appointments/${id}`,
          parseDetail,
        );
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
          <>
            <StaffCommandBar title="Intake appointment" />
            <div className={styles.direct}>
              <StaffLink className={styles.backLink} href={returnTo} prefetch={false}>
                Back to intake appointments
              </StaffLink>
              <Card>
                <AppointmentWorkbench key={id} id={id} actor={actor} initial={initial} />
              </Card>
            </div>
          </>
        );
      }}
    </StaffEntry>
  );
}
