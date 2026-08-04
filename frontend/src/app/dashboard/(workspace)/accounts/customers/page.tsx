import { AccountsPageEntry } from "@/components/accounts/AccountsPageEntry";
import type { SearchParams } from "@/lib/query";

export default function CustomerAccountsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return <AccountsPageEntry view="customers" searchParams={searchParams} />;
}
