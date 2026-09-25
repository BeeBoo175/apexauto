import type { Metadata } from "next";
import { readCurrentActor, readOwnAccount } from "@/lib/auth/server";
import { staffSellNotice } from "@/lib/intake/staff-sell-notice";
import { requestApiJson } from "@/lib/server/http/api";
import { readShowrooms } from "@/lib/guide/guide-reads";
import { parseTaxonomy } from "@/lib/intake/parse";
import { SellerJourney } from "@/components/sell/SellerJourney";

export const metadata: Metadata = {
  title: "Sell your car | ApexAuto",
  robots: { index: false, follow: false },
};

export default async function SellPage() {
  const [taxonomy, branches, access] = await Promise.all([
    requestApiJson("/api/taxonomy", parseTaxonomy),
    readShowrooms(),
    readOwnAccount().catch(() => ({ status: 502, account: null })),
  ]);
  // The customer account read refuses staff, so a refused read is the only case that needs the signed-in role.
  const staffNotice = access.status === 403 ? await readStaffSellNotice() : null;
  return (
    <SellerJourney
      initial={{
        taxonomy: taxonomy.data,
        branches: branches.data,
        account: access.account,
        access: access.status,
        staffNotice,
      }}
    />
  );
}

async function readStaffSellNotice() {
  const current = await readCurrentActor().catch(() => ({ actor: null }));
  return current.actor ? staffSellNotice(current.actor) : null;
}
