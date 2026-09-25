import type { Metadata } from "next";
import { InspectionStandards } from "@/components/guide/InspectionStandards";
import { StatusPage } from "@/components/ui/StatusPage";
import { faqEntries } from "@/lib/guide/faq-items";
import { readGuides } from "@/lib/guide/guide-reads";

// The checkpoint catalogue, the grading limits and the warranty plans change only with a release, so the page is
// generated ahead of time and refreshed hourly (PUBLIC_CONTENT_REVALIDATE_SECONDS).
export const revalidate = 3600;

export const metadata: Metadata = {
  title: "Inspection standards — ApexAuto",
  description:
    "How ApexAuto checks every car across core systems, grades repairs A/B/C, and blocks structural, flood, or tampered vehicles.",
};

export default async function InspectionStandardsPage() {
  const guides = await readGuides();
  if (!guides.data) {
    return (
      <StatusPage
        code={String(guides.status)}
        icon="alert-triangle"
        title="Inspection guide unavailable"
      />
    );
  }

  const { purchase, inspection: guide } = guides.data;

  // The same answers as the FAQ page, narrowed to the ones about inspection and grades.
  const faq = faqEntries(purchase, guide.grading).filter((entry) => entry.onInspectionPage);

  return <InspectionStandards guide={guide} warranties={purchase.warranties} faq={faq} />;
}
