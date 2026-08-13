import { AccountsPageEntry } from "@/components/accounts/AccountsPageEntry";
import type { SearchParams } from "@/lib/query";
import { workspaceMetadata } from "@/lib/staff/navigation";

export const metadata = workspaceMetadata("/dashboard/accounts");

export default function AccountsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return <AccountsPageEntry view="staff" searchParams={searchParams} />;
}
