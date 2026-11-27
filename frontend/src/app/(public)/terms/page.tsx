import type { Metadata } from "next";
import { LegalDocument } from "@/components/guide/LegalDocument";
import { readReservationHoldHours } from "@/lib/guide/guide-reads";
import { TERMS_INTRO, termsSections } from "@/lib/legal/terms-of-use";

// The hold length comes from the cached purchase guide, so the page is refreshed daily
// (PUBLIC_CONTENT_REVALIDATE_SECONDS).
export const revalidate = 86400;

export const metadata: Metadata = {
  title: "Terms of use — ApexAuto",
  description:
    "The terms for using the ApexAuto demonstration site: what it is, what is simulated and how you may use it.",
};

export default async function TermsPage() {
  const holdHours = await readReservationHoldHours();

  return (
    <LegalDocument title="Terms of use" intro={TERMS_INTRO} sections={termsSections(holdHours)} />
  );
}
