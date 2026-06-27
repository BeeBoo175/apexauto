import type { SearchParams } from "@/lib/query";
import { redirect } from "next/navigation";
import { AccountWorkspace } from "@/components/account/AccountWorkspace";
import { readOwnProfile } from "@/lib/account/server";
import { privateApiRead } from "@/lib/server/http/api";
import { parseAppointment } from "@/lib/intake/parse";
import { parsePage } from "@/lib/api";
import { historyParams, historyQuery } from "@/lib/intake/list-query";
import { ACCOUNT_PAGE_SIZE } from "@/lib/pagination";
import {
  accountReservationFilters,
  accountReservationPage,
} from "@/lib/vehicles/account-reservations";
import { readAccountReservations } from "@/lib/vehicles/account-reservations-server";
import { readTestDriveHistory } from "@/lib/test-drives/customer-server";
import { accountTestDriveParams } from "@/lib/test-drives/account";

export const metadata = {
  title: "Your account | ApexAuto",
  robots: { index: false, follow: false },
};

export default async function AccountPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const response = await readOwnProfile();

  if (response.status === 401) {
    redirect("/login");
  }

  if (!response.data) {
    throw new Error("Account service unavailable");
  }

  const profile = response.data;

  if (profile.role !== "Customer") {
    redirect("/dashboard/account");
  }

  const params = await searchParams;
  const { filters, page } = historyParams(params);
  const reservationPage = accountReservationPage(params);
  const reservationFilters = accountReservationFilters(params);
  const testDriveQuery = accountTestDriveParams(params);
  const [history, reservations, testDrives] = await Promise.all([
    profile.emailVerified
      ? privateApiRead(
          `/api/intake/appointments?${historyQuery(filters, page, ACCOUNT_PAGE_SIZE)}`,
          (value) => parsePage(value, parseAppointment, ACCOUNT_PAGE_SIZE),
        )
      : Promise.resolve(null),
    readAccountReservations(reservationPage, reservationFilters),
    readTestDriveHistory(testDriveQuery.page, testDriveQuery.filters),
  ]);
  if (reservations.data && reservations.data.actor.userId !== profile.userId) {
    throw new Error("Account identity could not be confirmed");
  }
  if (testDrives.data && testDrives.data.actor.userId !== profile.userId) {
    throw new Error("Account identity could not be confirmed");
  }
  return (
    <AccountWorkspace
      key={profile.userId}
      profile={profile}
      history={history}
      filters={filters}
      page={page}
      reservations={reservations}
      reservationPage={reservationPage}
      reservationFilters={reservationFilters}
      testDrives={testDrives}
      testDriveQuery={testDriveQuery}
    />
  );
}
