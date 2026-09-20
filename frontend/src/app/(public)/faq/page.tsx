import type { Metadata } from "next";
import { HelpCenter } from "@/components/guide/HelpCenter";
import { StatusPage } from "@/components/ui/StatusPage";
import { formatCurrency } from "@/lib/currency-format";
import { faqEntries } from "@/lib/guide/faq-items";
import { parseInspectionGuide } from "@/lib/guide/inspection-points";
import { parsePurchaseGuide } from "@/lib/guide/purchase-guide";
import { requestApiJson } from "@/lib/server/http/api";
import { RESERVATION_DEPOSIT } from "@/lib/vehicles/deposit-terms";

export const metadata: Metadata = {
  title: "FAQ — ApexAuto",
  description: `Quick answers on on-the-road pricing, ${formatCurrency(RESERVATION_DEPOSIT, "RM")} holds, 210-point grading, ApexShield warranties, trade-ins and showrooms, with the notices that apply to this site.`,
};

export default async function HelpPage() {
  const [purchase, inspection] = await Promise.all([
    requestApiJson("/api/purchase-guide", parsePurchaseGuide),
    requestApiJson("/api/inspection-guide", parseInspectionGuide),
  ]);
  if (!purchase.data || !inspection.data) {
    const failed = purchase.data ? inspection : purchase;
    return (
      <StatusPage code={String(failed.status)} icon="alert-triangle" title="Help unavailable" />
    );
  }

  return <HelpCenter faqEntries={faqEntries(purchase.data, inspection.data.grading)} />;
}
