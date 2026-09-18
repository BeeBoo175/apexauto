import type { Metadata } from "next";
import { InspectionStandards } from "@/components/guide/InspectionStandards";
import { StatusPage } from "@/components/ui/StatusPage";
import { faqEntries } from "@/lib/guide/faq-items";
import { parseInspectionGuide } from "@/lib/guide/inspection-points";
import { parsePurchaseGuide } from "@/lib/guide/purchase-guide";
import { requestApiJson } from "@/lib/server/http/api";

// The checkpoint catalogue, the grading limits and the warranty plans change only with a release, so the page is
// generated ahead of time and refreshed on this interval.
const REVALIDATE_SECONDS = 3600;
export const revalidate = 3600;

export const metadata: Metadata = {
  title: "Inspection standards — ApexAuto",
  description:
    "How ApexAuto checks every car across core systems, grades repairs A/B/C, and blocks structural, flood, or tampered vehicles.",
};

export default async function InspectionStandardsPage() {
  const options = { revalidateSeconds: REVALIDATE_SECONDS };
  const [result, purchase] = await Promise.all([
    requestApiJson("/api/inspection-guide", parseInspectionGuide, options),
    requestApiJson("/api/purchase-guide", parsePurchaseGuide, options),
  ]);
  const guide = result.data;
  if (!guide || !purchase.data) {
    return (
      <StatusPage
        code={String(guide ? purchase.status : result.status)}
        icon="alert-triangle"
        title="Inspection guide unavailable"
      />
    );
  }

  // The same answers as the FAQ page, narrowed to the ones about inspection and grades.
  const faq = faqEntries(purchase.data, guide.grading).filter((entry) => entry.onInspectionPage);

  return <InspectionStandards guide={guide} warranties={purchase.data.warranties} faq={faq} />;
}
