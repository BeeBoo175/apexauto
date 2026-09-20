import type { Metadata } from "next";
import { LegalDocument } from "@/components/guide/LegalDocument";
import { TERMS_INTRO, termsSections } from "@/lib/legal/terms-of-use";

export const metadata: Metadata = {
  title: "Terms of use — ApexAuto",
  description:
    "The terms for using the ApexAuto demonstration site: what it is, what is simulated and how you may use it.",
};

// Fixed text, so the page is generated ahead of time.
export default function TermsPage() {
  return <LegalDocument title="Terms of use" intro={TERMS_INTRO} sections={termsSections} />;
}
