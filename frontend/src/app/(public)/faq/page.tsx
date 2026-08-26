import type { Metadata } from "next";
import { FaqExplorer } from "@/components/guide/FaqExplorer";
import { GuideBand, GuideHubBanner } from "@/components/guide/GuidePageShell";

import { formatCurrency } from "@/lib/currency-format";
import { RESERVATION_DEPOSIT } from "@/lib/vehicles/deposit-terms";

export const metadata: Metadata = {
  title: "Frequently Asked Questions — ApexAuto",
  description: `Quick answers on OTR pricing, ${formatCurrency(RESERVATION_DEPOSIT, "RM")} holds, 210-point grading, ApexShield warranties, trade-ins, and branches.`,
};

export default function FaqPage() {
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
        <FaqExplorer />
      </GuideBand>
    </>
  );
}
