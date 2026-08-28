import type { Metadata } from "next";
import Link from "next/link";
import { BuyingGuideFinancingSection } from "@/components/guide/BuyingGuideFinancingSection";
import { BuyingGuideOverviewSection } from "@/components/guide/BuyingGuideOverviewSection";
import { BuyingGuidePricingSection } from "@/components/guide/BuyingGuidePricingSection";
import { BuyingGuideTradeInSection } from "@/components/guide/BuyingGuideTradeInSection";
import { BuyingGuideWarrantySection } from "@/components/guide/BuyingGuideWarrantySection";
import { GuideJumpBar } from "@/components/guide/GuideJumpBar";
import { GuideBand, GuideHubBanner } from "@/components/guide/GuidePageShell";
import { buttonClassName } from "@/components/ui/Button";
import { StatusPage } from "@/components/ui/StatusPage";
import { formatCurrency } from "@/lib/currency-format";
import { guideServiceTax, parsePurchaseGuide, sstPercentLabel } from "@/lib/guide/purchase-guide";
import { requestApiJson } from "@/lib/server/http/api";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import styles from "@/components/guide/BuyingGuideSections.module.css";

export const metadata: Metadata = {
  title: "Buying, Financing & ApexShield Guide — ApexAuto",
  description:
    "How buying works at ApexAuto plus on-the-road cost breakdowns, monthly loan estimates, trade-in prep, and grade-matched warranties.",
};

const jumpItems = [
  { id: "how-it-works", label: "How buying works" },
  { id: "pricing", label: "Pricing & hold deposit" },
  { id: "financing", label: "Financing estimator" },
  { id: "trade-in", label: "Trade-in prep" },
  { id: "warranty", label: "ApexShield warranty" },
] as const;

export default async function BuyingGuidePage() {
  const result = await requestApiJson("/api/purchase-guide", parsePurchaseGuide);
  const guide = result.data;
  if (!guide) {
    return (
      <StatusPage
        code={String(result.status)}
        icon="alert-triangle"
        title="Buying guide unavailable"
      />
    );
  }

  return (
    <>
      <GuideHubBanner
        title="Buying, financing & ApexShield guide"
        description="See how purchasing works, estimate OTR costs and loans, and compare warranty coverage."
        actions={[
          { href: "#pricing", label: "Calculate OTR price", variant: "primary" },
          { href: "/vehicles", label: "Browse cars for sale", variant: "shell" },
        ]}
      />

      <GuideJumpBar label="Buying guide sections" items={jumpItems} />

      <GuideBand
        id="how-it-works"
        surface="paper"
        headingId="how-it-works-heading"
        heading="How buying from ApexAuto works"
        description="Four clear steps from online inspection review to showroom handover."
      >
        <BuyingGuideOverviewSection guide={guide} />
      </GuideBand>

      <GuideBand
        id="pricing"
        surface="canvas"
        headingId="pricing-heading"
        heading="Transparent on-the-road pricing"
        description={`Fixed price, ${formatCurrency(guide.processingFee, "RM")} fee, road tax, and ${formatCurrency(guideServiceTax(guide), "RM")} SST (${sstPercentLabel(guide.sstRate)}). Insurance and optional warranty stay separate.`}
      >
        <BuyingGuidePricingSection guide={guide} />
      </GuideBand>

      <GuideBand
        id="financing"
        surface="paper"
        headingId="financing-heading"
        heading="Monthly repayment estimator"
        description="Illustrative 3% flat annual rate across 1 to 9 years."
      >
        <BuyingGuideFinancingSection />
      </GuideBand>

      <GuideBand
        id="trade-in"
        surface="canvas"
        headingId="trade-in-heading"
        heading="Trade-in preparation"
        description="Confirm your online guide range during a 90-minute branch slot."
      >
        <BuyingGuideTradeInSection />
      </GuideBand>

      <GuideBand
        id="warranty"
        surface="paper"
        headingId="warranty-heading"
        heading="ApexShield warranty by inspection grade"
        description="Optional grade-matched coverage, billed separately from OTR pricing."
      >
        <BuyingGuideWarrantySection warranties={guide.warranties} />
        <div className={styles.guideClosingCta}>
          <div className={styles.guideClosingCopy}>
            <h3 className={layout.cardHeading}>Ready to reserve your car?</h3>
            <p>Browse live stock with full 210-point inspection grades and fixed OTR numbers.</p>
          </div>
          <div className={styles.guideClosingActions}>
            <Link href="/vehicles" prefetch={false} className={buttonClassName("primary", "md")}>
              Browse inspected cars
            </Link>
            <Link href="/faq" className={buttonClassName("secondary", "md")}>
              Read FAQ
            </Link>
          </div>
        </div>
      </GuideBand>
    </>
  );
}
