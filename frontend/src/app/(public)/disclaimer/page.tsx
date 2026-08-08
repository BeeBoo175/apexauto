import type { Metadata } from "next";
import { AssetAttributionList, DisclaimerNoticesGrid } from "@/components/guide/DisclaimerExplorer";
import { GuideBand, GuideHubBanner } from "@/components/guide/GuidePageShell";

export const metadata: Metadata = {
  title: "Disclaimers & Asset Credits — ApexAuto",
  description:
    "Scope boundaries for simulated holds, calculators, 210-point inspections, fixture branches, and media credits.",
};

export default function DisclaimerPage() {
  return (
    <>
      <GuideHubBanner
        title="Disclaimers & asset credits"
        description="Scope terms for simulated holds, calculators, inspections, and media credits."
        actions={[
          { href: "#credits", label: "Media attributions", variant: "primary" },
          { href: "/vehicles", label: "Browse cars for sale", variant: "shell" },
        ]}
      />

      <GuideBand
        surface="canvas"
        headingId="disclosures-heading"
        heading="Platform scope & boundaries"
      >
        <DisclaimerNoticesGrid />
      </GuideBand>

      <GuideBand
        id="credits"
        surface="paper"
        headingId="credits-heading"
        heading="Photography and UI icon credits"
        description="Sources and license attributions for storefront photography, map boundaries, and icons."
      >
        <AssetAttributionList />
      </GuideBand>
    </>
  );
}
