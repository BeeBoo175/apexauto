import { redirect } from "next/navigation";
import { AccountWorkspace } from "@/components/account/AccountWorkspace";
import { readOwnAccount, readCurrentActor } from "@/lib/auth/server";
import { actorDestination } from "@/lib/auth/actor";
import { privateApiRead } from "@/lib/server/http/api";
import { parseAppointment } from "@/lib/intake/parse";
import { parsePage } from "@/lib/api";
import { historyParams, historyQuery } from "@/lib/intake/list-query";

export const metadata = {
  title: "Your account | ApexAuto",
  robots: { index: false, follow: false },
};

export default async function AccountPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const response = await readOwnAccount();

  if (response.status === 403) {
    const current = await readCurrentActor();
    if (current.actor && current.actor.role !== "Customer") {
      redirect(actorDestination(current.actor));
    }
  }
  if (response.status === 401) {
    redirect("/login");
  }

  if (!response.account) {
    throw new Error("Account service unavailable");
  }

  const { filters, page } = historyParams(await searchParams);
  const history = await privateApiRead(
    `/api/intake/appointments?${historyQuery(filters, page)}`,
    (value) => parsePage(value, parseAppointment),
  );
  return (
    <AccountWorkspace
      key={response.account.userId}
      account={response.account}
      history={history}
      filters={filters}
      page={page}
    />
  );
}
