import type { Metadata } from "next";
import { ApexShieldGuide } from "@/components/guide/ApexShieldGuide";
import { faqEntries } from "@/lib/guide/faq-items";
import { requireGuides } from "@/lib/guide/guide-reads";

// Guide content is cached for a day (PUBLIC_CONTENT_REVALIDATE_SECONDS).
export const revalidate = 86400;

export const metadata: Metadata = {
  title: "ApexShield Warranty | ApexAuto Malaysia",
  description:
    "Compare the ApexShield warranty plans and see which ones a car's inspection grade can take. Optional cover, billed separately from the on-the-road price.",
};

export default async function WarrantyPage() {
  const { purchase, inspection } = await requireGuides();
  const faq = faqEntries(purchase, inspection.grading).filter((entry) => entry.onWarrantyPage);

  return <ApexShieldGuide warranties={purchase.warranties} faq={faq} />;
}
