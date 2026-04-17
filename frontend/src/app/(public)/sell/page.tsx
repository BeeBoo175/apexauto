import { paginationQuery } from "@/lib/pagination";
import type { Metadata } from "next";
import { readOwnAccount } from "@/lib/auth/server";
import { requestApiJson } from "@/lib/server/http/api";
import { parseBranch } from "@/lib/branches";
import { parsePage } from "@/lib/api";
import { parseTaxonomy } from "@/lib/intake/parse";
import { SellerJourney } from "@/components/sell/SellerJourney";

export const metadata: Metadata = {
  title: "Sell your car | ApexAuto",
  robots: { index: false, follow: false },
};

export default async function SellPage() {
  const [taxonomy, branches, access] = await Promise.all([
    requestApiJson("/api/taxonomy", parseTaxonomy),
    requestApiJson(`/api/branches?${paginationQuery()}`, (value) => parsePage(value, parseBranch)),
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
