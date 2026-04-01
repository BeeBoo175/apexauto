import { StaffEntry } from "@/components/staff/StaffEntry";
import { ReservationTable } from "@/components/vehicles/ReservationTable";
import { actorIdentity } from "@/lib/auth/actor";
import { privateApiRead } from "@/lib/server/http/api";
import { staffBranches } from "@/lib/staff/server";
import {
  hasReservationAccess,
  reservationParams,
  reservationQuery,
} from "@/lib/vehicles/reservations";
import { parseReservations } from "@/lib/vehicles/parse-reservations";
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
        const { filters, page } = reservationParams(await searchParams);
        const [initial, branches] = await Promise.all([
          privateApiRead(
            `/api/staff/reservations?${reservationQuery(filters, page)}`,
            parseReservations,
          ),
          staffBranches(actor),
        ]);
        return (
          <ReservationTable
            key={actorIdentity(actor)}
            actor={actor}
            initial={initial}
            filters={filters}
            page={page}
            branches={branches?.data ?? null}
          />
        );
      }}
    </StaffEntry>
  );
}
