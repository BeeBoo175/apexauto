import { AccountsPageEntry } from "@/components/accounts/AccountsPageEntry";
import type { SearchParams } from "@/lib/query";

export default function AccountsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return <AccountsPageEntry view="staff" searchParams={searchParams} />;
}
