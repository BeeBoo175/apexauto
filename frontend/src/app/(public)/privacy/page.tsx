import type { Metadata } from "next";
import { LegalDocument } from "@/components/guide/LegalDocument";
import { PRIVACY_INTRO, privacySections } from "@/lib/legal/privacy-policy";

export const metadata: Metadata = {
  title: "Privacy policy — ApexAuto",
  description:
    "What personal data the ApexAuto demonstration site collects, why, how long it is kept and what you can do about it.",
};

// Fixed text, so the page is generated ahead of time.
export default function PrivacyPage() {
  return <LegalDocument title="Privacy policy" intro={PRIVACY_INTRO} sections={privacySections} />;
}
