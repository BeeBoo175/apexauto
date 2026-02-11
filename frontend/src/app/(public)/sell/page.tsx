import type { Metadata } from "next";
import { readOwnAccount } from "@/lib/auth/server";
import { intakeRead } from "@/lib/intake/server";
import { parseBranch, parsePage, parseTaxonomy } from "@/lib/intake/parse";
import { SellerJourney } from "@/components/sell/SellerJourney";

export const metadata: Metadata = {
  title: "Sell your car | ApexAuto",
  robots: { index: false, follow: false },
};

export default async function SellPage() {
  const [taxonomy, branches, access] = await Promise.all([
    intakeRead("/api/taxonomy", parseTaxonomy),
    intakeRead("/api/branches?page=1&pageSize=20", (value) => parsePage(value, parseBranch)),
    readOwnAccount().catch(() => ({ status: 502, account: null })),
  ]);
  return (
    <SellerJourney
      initial={{
        taxonomy: taxonomy.data,
        branches: branches.data,
        account: access.account,
        access: access.status,
      }}
    />
  );
}
