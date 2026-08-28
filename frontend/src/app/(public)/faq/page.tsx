import type { Metadata } from "next";
import { FaqExplorer } from "@/components/guide/FaqExplorer";
import { StatusPage } from "@/components/ui/StatusPage";
import { GuideBand, GuideHubBanner } from "@/components/guide/GuidePageShell";

import { formatCurrency } from "@/lib/currency-format";
import { faqEntries } from "@/lib/guide/faq-items";
import { parseInspectionGuide } from "@/lib/guide/inspection-points";
import { parsePurchaseGuide } from "@/lib/guide/purchase-guide";
import { requestApiJson } from "@/lib/server/http/api";
import { RESERVATION_DEPOSIT } from "@/lib/vehicles/deposit-terms";

export const metadata: Metadata = {
  title: "Frequently Asked Questions — ApexAuto",
  description: `Quick answers on OTR pricing, ${formatCurrency(RESERVATION_DEPOSIT, "RM")} holds, 210-point grading, ApexShield warranties, trade-ins, and branches.`,
};

export default async function FaqPage() {
  const [purchase, inspection] = await Promise.all([
    requestApiJson("/api/purchase-guide", parsePurchaseGuide),
    requestApiJson("/api/inspection-guide", parseInspectionGuide),
  ]);
  if (!purchase.data || !inspection.data) {
    const failed = purchase.data ? inspection : purchase;
    return (
      <StatusPage code={String(failed.status)} icon="alert-triangle" title="FAQ unavailable" />
    );
  }

  return (
    <>
      <GuideHubBanner
        title="Frequently asked questions"
        description="Quick answers on pricing, holds, inspection grades, and branch visits."
        actions={[
          { href: "/vehicles", label: "Browse cars for sale", variant: "primary" },
          { href: "/buying-guide", label: "Open buying guide", variant: "shell", prefetch: true },
        ]}
      />

      <GuideBand surface="canvas" headingId="faq-list-heading">
        <FaqExplorer faqEntries={faqEntries(purchase.data, inspection.data.grading)} />
      </GuideBand>
    </>
  );
}
