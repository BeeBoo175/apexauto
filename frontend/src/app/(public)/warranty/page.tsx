import type { Metadata } from "next";
import { ApexShieldGuide } from "@/components/guide/ApexShieldGuide";
import { StatusPage } from "@/components/ui/StatusPage";
import { faqEntries } from "@/lib/guide/faq-items";
import { readGuides } from "@/lib/guide/guide-reads";

// Guide content is cached for an hour (PUBLIC_CONTENT_REVALIDATE_SECONDS).
export const revalidate = 3600;

export const metadata: Metadata = {
  title: "ApexShield Warranty | ApexAuto Malaysia",
  description:
    "Compare the ApexShield warranty plans and see which ones a car's inspection grade can take. Optional cover, billed separately from the on-the-road price.",
};

export default async function WarrantyPage() {
  const guides = await readGuides();
  if (!guides.data) {
    return (
      <StatusPage code={String(guides.status)} icon="alert-triangle" title="Warranty unavailable" />
    );
  }

  const { purchase, inspection } = guides.data;
  const faq = faqEntries(purchase, inspection.grading).filter((entry) => entry.onWarrantyPage);

  return <ApexShieldGuide warranties={purchase.warranties} faq={faq} />;
}
