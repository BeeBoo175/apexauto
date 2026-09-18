import type { Metadata } from "next";
import { ApexShieldGuide } from "@/components/guide/ApexShieldGuide";
import { StatusPage } from "@/components/ui/StatusPage";
import { faqEntries } from "@/lib/guide/faq-items";
import { parseInspectionGuide } from "@/lib/guide/inspection-points";
import { parsePurchaseGuide } from "@/lib/guide/purchase-guide";
import { requestApiJson } from "@/lib/server/http/api";

export const metadata: Metadata = {
  title: "ApexShield Warranty | ApexAuto Malaysia",
  description:
    "Compare the ApexShield warranty plans and see which ones a car's inspection grade can take. Optional cover, billed separately from the on-the-road price.",
};

export default async function WarrantyPage() {
  const [purchase, inspection] = await Promise.all([
    requestApiJson("/api/purchase-guide", parsePurchaseGuide),
    requestApiJson("/api/inspection-guide", parseInspectionGuide),
  ]);
  if (!purchase.data || !inspection.data) {
    const failed = purchase.data ? inspection : purchase;
    return (
      <StatusPage code={String(failed.status)} icon="alert-triangle" title="Warranty unavailable" />
    );
  }

  const faq = faqEntries(purchase.data, inspection.data.grading).filter(
    (entry) => entry.onWarrantyPage,
  );

  return <ApexShieldGuide warranties={purchase.data.warranties} faq={faq} />;
}
