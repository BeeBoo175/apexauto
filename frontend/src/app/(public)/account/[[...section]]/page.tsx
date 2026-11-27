import type { SearchParams } from "@/lib/query";
import { notFound, redirect } from "next/navigation";
import { AccountWorkspace } from "@/components/customer-account/AccountWorkspace";
import { sectionFromSegments } from "@/lib/profile/sections";
import { readOwnProfile } from "@/lib/profile/server";
import { historyParams } from "@/lib/intake/list-query";
import {
  accountReservationFilters,
  accountReservationPage,
} from "@/lib/vehicles/account-reservations";
import { accountTestDriveParams } from "@/lib/test-drives/account";

export const metadata = {
  title: "Your account | ApexAuto",
  robots: { index: false, follow: false },
};

// The server only confirms the signed-in customer. Each section reads its own data in the browser under the
// shared progress bar, so the account needs no search-engine snapshot and a section never waits on the others.
export default async function AccountPage({
  params,
  searchParams,
}: {
  params: Promise<{ section?: string[] }>;
  searchParams: Promise<SearchParams>;
}) {
  const section = sectionFromSegments((await params).section);
  if (!section) {
    notFound();
  }
  const response = await readOwnProfile();

  if (response.status === 401) {
    redirect("/login");
  }

  if (!response.data) {
    throw new Error("Account service unavailable");
  }

  const profile = response.data;

  if (profile.role !== "Customer") {
    redirect("/dashboard/profile");
  }

  const entry = entryParameters(section, await searchParams);
  return (
    <AccountWorkspace
      key={profile.userId}
      profile={profile}
      section={section}
      appointments={entry.appointments}
      reservations={entry.reservations}
      testDrives={entry.testDrives}
      collection={entry.collection}
    />
  );
}

// Address parameters belong to the section the path names; every other section starts on its first page.
function entryParameters(
  section: NonNullable<ReturnType<typeof sectionFromSegments>>,
  query: SearchParams,
) {
  return {
    appointments: section === "appointments" ? historyParams(query) : null,
    reservations:
      section === "reservations"
        ? { page: accountReservationPage(query), filters: accountReservationFilters(query) }
        : null,
    testDrives: section === "test-drives" ? accountTestDriveParams(query) : null,
    collection:
      section === "awaiting-collection"
        ? { page: accountReservationPage(query), filters: accountReservationFilters(query) }
        : null,
  };
}
