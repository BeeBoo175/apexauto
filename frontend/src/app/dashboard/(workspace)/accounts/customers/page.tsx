import { AccountsPageEntry } from "@/components/accounts/AccountsPageEntry";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/accounts");

export default function CustomerAccountsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return <AccountsPageEntry view="customers" searchParams={searchParams} />;
}
