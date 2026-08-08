import type { Metadata } from "next";
import { BuyingGuideFinancingSection } from "@/components/guide/BuyingGuideFinancingSection";
import { BuyingGuidePricingSection } from "@/components/guide/BuyingGuidePricingSection";
import { BuyingGuideTradeInSection } from "@/components/guide/BuyingGuideTradeInSection";
import { BuyingGuideWarrantySection } from "@/components/guide/BuyingGuideWarrantySection";
import { GuideJumpBar } from "@/components/guide/GuideJumpBar";
import { GuideBand, GuideHubBanner } from "@/components/guide/GuidePageShell";

export const metadata: Metadata = {
  title: "Buying, Financing & ApexShield Guide — ApexAuto",
  description:
    "Estimate on-the-road costs, monthly repayments, trade-in prep, and grade-matched warranties at ApexAuto.",
};

const jumpItems = [
  { id: "pricing", label: "Pricing & hold deposit" },
  { id: "financing", label: "Financing estimator" },
  { id: "trade-in", label: "Trade-in prep" },
  { id: "warranty", label: "ApexShield warranty" },
] as const;

export default function BuyingGuidePage() {
  return (
    <>
      <GuideHubBanner
        title="Buying, financing & ApexShield guide"
        description="Plan on-the-road costs, monthly repayments, trade-ins, and warranty coverage."
        actions={[
          { href: "#pricing", label: "Calculate OTR price", variant: "primary" },
          { href: "/vehicles", label: "Browse cars for sale", variant: "shell" },
        ]}
      />

      <GuideJumpBar label="Buying guide sections" items={jumpItems} />

      <GuideBand
        id="pricing"
        surface="paper"
        headingId="pricing-heading"
        heading="Transparent on-the-road pricing"
        description="Fixed price, RM 500 fee, road tax, and RM 40 SST. Insurance and optional warranty stay separate."
      >
        <BuyingGuidePricingSection />
      </GuideBand>

      <GuideBand
        id="financing"
        surface="canvas"
        headingId="financing-heading"
        heading="Monthly repayment estimator"
        description="Illustrative 3% flat annual rate across 1 to 9 years."
      >
        <BuyingGuideFinancingSection />
      </GuideBand>

      <GuideBand
        id="trade-in"
        surface="paper"
        headingId="trade-in-heading"
        heading="Trade-in preparation"
        description="Confirm your online guide range during a 90-minute branch slot."
      >
        <BuyingGuideTradeInSection />
      </GuideBand>

      <GuideBand
        id="warranty"
        surface="canvas"
        headingId="warranty-heading"
        heading="ApexShield warranty by inspection grade"
        description="Optional grade-matched coverage, billed separately from OTR pricing."
      >
        <BuyingGuideWarrantySection />
      </GuideBand>
    </>
  );
}
