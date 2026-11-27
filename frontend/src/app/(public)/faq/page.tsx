import type { Metadata } from "next";
import { HelpCenter } from "@/components/guide/HelpCenter";
import { formatCurrency } from "@/lib/currency-format";
import { faqEntries } from "@/lib/guide/faq-items";
import { requireGuides } from "@/lib/guide/guide-reads";
import { RESERVATION_DEPOSIT } from "@/lib/vehicles/deposit-terms";

// Guide content is cached for a day (PUBLIC_CONTENT_REVALIDATE_SECONDS).
export const revalidate = 86400;

export const metadata: Metadata = {
  title: "FAQ — ApexAuto",
  description: `Quick answers on on-the-road pricing, ${formatCurrency(RESERVATION_DEPOSIT, "RM")} holds, 210-point grading, ApexShield warranties, trade-ins and showrooms, with the notices that apply to this site.`,
};

export default async function HelpPage() {
  const { purchase, inspection } = await requireGuides();

  return <HelpCenter faqEntries={faqEntries(purchase, inspection.grading)} />;
}
