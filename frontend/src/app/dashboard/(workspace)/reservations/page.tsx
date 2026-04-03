import { StaffEntry } from "@/components/staff/StaffEntry";
import { ReservationClientEntry } from "@/components/vehicles/ReservationClientEntry";
import { hasReservationAccess } from "@/lib/vehicles/reservations";
import type { SearchParams } from "@/lib/query";

export default async function ReservationsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return (
    <StaffEntry
      path="/dashboard/reservations"
      access={hasReservationAccess}
      deniedTitle="Reservation access unavailable"
      deniedDescription="Only Branch Managers and General Managers can view reservations."
    >
      {async (actor) => {
        return (
          <ReservationClientEntry
            key={crypto.randomUUID()}
            actor={actor}
            parameters={await searchParams}
          />
        );
      }}
    </StaffEntry>
  );
}
