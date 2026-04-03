import { StaffEntry } from "@/components/staff/StaffEntry";
import { AppointmentClientEntry } from "@/components/staff/AppointmentClientEntry";
import { notFound } from "next/navigation";
import { isUuid } from "@/lib/auth/contracts";
import { hasIntakeAccess } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { staffParams, staffViewQuery } from "@/lib/staff/query";
import { calendarHref, calendarParams } from "@/lib/staff/calendar";

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
        const navigation = await searchParams;
        const { filters, page } = staffParams(navigation);
        const returnTo =
          navigation.view === "list"
            ? `/dashboard/intake?${staffViewQuery(filters, page)}`
            : calendarHref(calendarParams(navigation));
        return (
          <AppointmentClientEntry
            key={crypto.randomUUID()}
            id={id}
            actor={actor}
            returnTo={returnTo}
          />
        );
      }}
    </StaffEntry>
  );
}
