import { notFound } from "next/navigation";
import { AccountsPageEntry } from "@/components/accounts/AccountsPageEntry";
import { accountSelectionRoute } from "@/lib/accounts/accounts-location";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/accounts");

export default async function SelectedStaffAccountPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string; section?: string[] }>;
  searchParams: Promise<SearchParams>;
}) {
  const { id, section } = await params;
  const selection = accountSelectionRoute("staff", id, section);
  if (!selection) {
    notFound();
  }
  return <AccountsPageEntry view="staff" selection={selection} searchParams={searchParams} />;
}
