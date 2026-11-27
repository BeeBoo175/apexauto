import type { Metadata } from "next";
import Link from "next/link";
import { HelpCenter } from "@/components/guide/HelpCenter";
import { buttonClassName } from "@/components/ui/Button";
import { StatusPage } from "@/components/ui/StatusPage";
import { formatCurrency } from "@/lib/currency-format";
import { faqEntries } from "@/lib/guide/faq-items";
import { readGuides } from "@/lib/guide/guide-reads";
import { RESERVATION_DEPOSIT } from "@/lib/vehicles/deposit-terms";

// Guide content is cached for an hour (PUBLIC_CONTENT_REVALIDATE_SECONDS).
export const revalidate = 3600;

export const metadata: Metadata = {
  title: "FAQ — ApexAuto",
  description: `Quick answers on on-the-road pricing, ${formatCurrency(RESERVATION_DEPOSIT, "RM")} holds, 210-point grading, ApexShield warranties, trade-ins and showrooms, with the notices that apply to this site.`,
};

export default async function HelpPage() {
  const guides = await readGuides();
  if (!guides.data) {
    return (
      <StatusPage
        code={String(guides.status)}
        icon="alert-triangle"
        tone="alert"
        title="Help unavailable"
        actions={
          <Link href="/" className={buttonClassName("secondary")}>
            Back to home
          </Link>
        }
      >
        Reload the page to try again.
      </StatusPage>
    );
  }

  const { purchase, inspection } = guides.data;

  return <HelpCenter faqEntries={faqEntries(purchase, inspection.grading)} />;
}
