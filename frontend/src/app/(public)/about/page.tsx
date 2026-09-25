import type { Metadata } from "next";
import { AboutUs } from "@/components/guide/AboutUs";
import { readReservationHoldHours } from "@/lib/guide/guide-reads";

// The hold length comes from the cached purchase guide, so the page is refreshed hourly
// (PUBLIC_CONTENT_REVALIDATE_SECONDS).
export const revalidate = 3600;

export const metadata: Metadata = {
  title: "About ApexAuto | Inspected Used Cars in Malaysia",
  description:
    "The story behind ApexAuto: what we believe about inspecting, repairing and selling used cars, and the milestones along the way.",
};

export default async function AboutPage() {
  const holdHours = await readReservationHoldHours();

  return <AboutUs holdHours={holdHours} />;
}
