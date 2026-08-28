import type { Metadata } from "next";
import Link from "next/link";
import styles from "@/components/guide/GuideInspection.module.css";
import { GuideJumpBar } from "@/components/guide/GuideJumpBar";
import { GuideBand, GuideHubBanner } from "@/components/guide/GuidePageShell";
import { InspectionGradingSimulator } from "@/components/guide/InspectionGradingSimulator";
import { InspectionInteractiveGuide } from "@/components/guide/InspectionInteractiveGuide";
import { buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import type { IconName } from "@/lib/icons";
import { StatusPage } from "@/components/ui/StatusPage";
import { formatCurrency } from "@/lib/currency-format";
import { requestApiJson } from "@/lib/server/http/api";
import { parseInspectionGuide } from "@/lib/guide/inspection-points";
import { inspectionCategories, signOffInvariants } from "@/lib/guide/inspection-rules";

export const metadata: Metadata = {
  title: "Inspection standards — ApexAuto",
  description:
    "How ApexAuto checks every car across core systems, grades repairs A/B/C, and blocks structural, flood, or tampered vehicles.",
};

const certificationWorkflow: readonly {
  step: string;
  icon: IconName;
  title: string;
  copy: string;
}[] = [
  {
    step: "01",
    icon: "inspection",
    title: "Physical & OBD diagnostic scan",
    copy: "Inspectors verify chassis measurements, paint depth, fault codes, and road-test braking at the showroom bay.",
  },
  {
    step: "02",
    icon: "tool",
    title: "Workshop reconditioning & proof",
    copy: "Every Minor or Major repair item is priced, rectified before listing, and logged against the stock record.",
  },
  {
    step: "03",
    icon: "shield-check",
    title: "Grade assignment & buyer sign-off",
    copy: "Cars earn Grade A, B, or C with matching ApexShield eligibility while instant-fail units are permanently withheld.",
  },
];

export default async function InspectionStandardsPage() {
  const result = await requestApiJson("/api/inspection-guide", parseInspectionGuide);
  const guide = result.data;
  if (!guide) {
    return (
      <StatusPage
        code={String(result.status)}
        icon="alert-triangle"
        title="Inspection guide unavailable"
      />
    );
  }
  const categories = inspectionCategories(guide.points);
  const maximumRepair = formatCurrency(guide.grading.maximumRepairCost);
  const jumpItems = [
    { id: "guarantees", label: "Zero-tolerance promise" },
    { id: "zones", label: `${categories.length} vehicle systems` },
    { id: "catalog", label: `${guide.points.length}-point checklist` },
    { id: "grading", label: "Grade simulator" },
  ] as const;

  const inspectionMetrics = [
    {
      value: String(guide.points.length),
      label: "Inspection checkpoints",
      hint: "Across structure, drivetrain, electrical, interior, and road test",
    },
    {
      value: String(categories.length),
      label: "Core vehicle systems",
      hint: "Signed off item by item before any listing is approved",
    },
    {
      value: "A / B / C",
      label: "Published condition grades",
      hint: "Determined strictly by worst severity and verified repair total",
    },
    {
      value: maximumRepair,
      label: "Hard repair cap",
      hint: `Cars exceeding ${maximumRepair} in needed work never reach sale`,
    },
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
      title: `Verified mileage & ${maximumRepair} cap`,
      copy: `Odometer tampering or repair costs above ${maximumRepair} fail at intake.`,
    },
  ];

  const invariants = signOffInvariants(guide.points.length);

  return (
    <>
      <GuideHubBanner
        title={`${guide.points.length}-point inspection standards`}
        description={`Every car passes ${categories.length} core system checks before earning Grade A, B, or C.`}
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

        <dl className={styles.kpiRibbon} aria-label="ApexAuto inspection standards at a glance">
          {inspectionMetrics.map((metric) => (
            <div key={metric.label} className={styles.kpiCard}>
              <dt className={styles.kpiLabel}>{metric.label}</dt>
              <dd className={styles.kpiValue}>{metric.value}</dd>
              <span className={styles.kpiHint}>{metric.hint}</span>
            </div>
          ))}
        </dl>

        <div className={styles.workflowBlock}>
          <h3 className={styles.workflowHeading}>How every car earns certification</h3>
          <ol className={styles.workflowGrid}>
            {certificationWorkflow.map((item) => (
              <li key={item.step} className={styles.workflowCard}>
                <div className={styles.workflowStepHead}>
                  <span className={styles.workflowStepNumber} aria-hidden="true">
                    {item.step}
                  </span>
                  <span className={styles.workflowStepIcon} aria-hidden="true">
                    <Icon name={item.icon} />
                  </span>
                </div>
                <h4 className={layout.cardHeading}>{item.title}</h4>
                <p>{item.copy}</p>
              </li>
            ))}
          </ol>
        </div>
      </GuideBand>

      <InspectionInteractiveGuide
        points={guide.points}
        categories={categories}
        gradingSection={
          <GuideBand
            id="grading"
            surface="canvas"
            headingId="grading-heading"
            heading="How Grades A, B, and C work"
            description="Select a grade tier or adjust inputs below to test how final grading is decided."
          >
            <InspectionGradingSimulator policy={guide.grading} pointCount={guide.points.length} />
            <div className={styles.signOffStrip} aria-label="Inspection sign-off standards">
              {invariants.map((item) => (
                <div key={item.title} className={styles.signOffItem}>
                  <div className={styles.signOffHeading}>
                    <Icon name={item.icon} />
                    <strong>{item.title}</strong>
                  </div>
                  <p>{item.copy}</p>
                </div>
              ))}
            </div>
            <div className={styles.inspectionClosingCta}>
              <div className={styles.inspectionClosingCopy}>
                <h3 className={layout.cardHeading}>Ready to compare graded stock?</h3>
                <p>Filter live inventory across Selangor and Pulau Pinang by Grade A, B, or C.</p>
              </div>
              <Link href="/vehicles" prefetch={false} className={buttonClassName("primary", "md")}>
                Browse inspected vehicles
              </Link>
            </div>
          </GuideBand>
        }
      />
    </>
  );
}
