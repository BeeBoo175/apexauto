import type { Metadata } from "next";
import styles from "@/components/guide/GuideInspection.module.css";
import { GuideJumpBar } from "@/components/guide/GuideJumpBar";
import { GuideBand, GuideHubBanner } from "@/components/guide/GuidePageShell";
import { InspectionCatalogBrowser } from "@/components/guide/InspectionCatalogBrowser";
import { InspectionGradingSimulator } from "@/components/guide/InspectionGradingSimulator";
import { InspectionZoneShowcase } from "@/components/guide/InspectionZoneShowcase";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import type { IconName } from "@/lib/icons";
import { signOffInvariants } from "@/lib/guide/inspection-rules";

export const metadata: Metadata = {
  title: "210-Point Inspection — ApexAuto",
  description:
    "How ApexAuto checks every car across five core systems, grades repairs A/B/C, and blocks structural, flood, or tampered vehicles.",
};

const jumpItems = [
  { id: "guarantees", label: "Zero-tolerance promise" },
  { id: "zones", label: "5 vehicle systems" },
  { id: "grading", label: "Grade simulator" },
  { id: "catalog", label: "210-point checklist" },
] as const;

const zeroTolerancePillars: readonly { icon: IconName; title: string; copy: string }[] = [
  {
    icon: "alert-triangle",
    title: "No cut-and-weld frames",
    copy: "Zero chassis distortion or structural cutting.",
  },
  {
    icon: "droplet-off",
    title: "Zero flood damage",
    copy: "Silt, waterlines, or trapped cavity moisture fail immediately.",
  },
  {
    icon: "gauge",
    title: "Verified mileage & RM 15k cap",
    copy: "Disqualified on tampered mileage or repairs above RM 15,000.",
  },
];

export default function InspectionStandardsPage() {
  return (
    <>
      <GuideHubBanner
        title="210-point inspection standards"
        description="Every car passes five core system checks before earning Grade A, B, or C."
        actions={[
          { href: "/vehicles", label: "Browse inspected cars", variant: "primary" },
          { href: "#grading", label: "Test grading rules", variant: "shell" },
        ]}
      />

      <GuideJumpBar label="Inspection guide sections" items={jumpItems} />

      <GuideBand
        id="guarantees"
        surface="paper"
        headingId="guarantees-heading"
        heading="Zero tolerance for structural, flood, or mileage fraud"
        description="Separate from statutory Puspakom and B5 ownership-transfer checks."
      >
        <div className={styles.guaranteeGrid}>
          {zeroTolerancePillars.map((item) => (
            <div key={item.title} className={styles.guaranteeItem}>
              <span className={styles.guaranteeIcon} aria-hidden="true">
                <Icon name={item.icon} />
              </span>
              <h3 className={layout.cardHeading}>{item.title}</h3>
              <p>{item.copy}</p>
            </div>
          ))}
        </div>
      </GuideBand>

      <GuideBand
        id="zones"
        surface="canvas"
        headingId="zones-heading"
        heading="210 checks across five systems"
      >
        <InspectionZoneShowcase />
      </GuideBand>

      <GuideBand
        id="grading"
        surface="paper"
        headingId="grading-heading"
        heading="How Grades A, B, and C work"
        description="Final grade follows whichever rule is stricter: the worst issue found or total repair cost."
      >
        <InspectionGradingSimulator />
        <div className={styles.signOffStrip} aria-label="Inspection sign-off standards">
          {signOffInvariants.map((item) => (
            <div key={item.title} className={styles.signOffItem}>
              <div className={styles.signOffHeading}>
                <Icon name={item.icon} />
                <strong>{item.title}</strong>
              </div>
              <p>{item.copy}</p>
            </div>
          ))}
        </div>
      </GuideBand>

      <GuideBand
        id="catalog"
        surface="canvas"
        headingId="catalog-heading"
        heading="Full 210-point checklist"
        description="Search by keyword or checkpoint code, or switch between vehicle systems."
      >
        <InspectionCatalogBrowser />
      </GuideBand>
    </>
  );
}
