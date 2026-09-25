import type { Metadata } from "next";
import { BuyingJourney } from "@/components/guide/BuyingJourney";
import { readReservationHoldHours } from "@/lib/guide/guide-reads";

// The hold length comes from the cached purchase guide, so the page is refreshed hourly
// (PUBLIC_CONTENT_REVALIDATE_SECONDS). Live prices stay on the car pages.
export const revalidate = 3600;

export async function generateMetadata(): Promise<Metadata> {
  const holdHours = await readReservationHoldHours();

  return {
    title: "How Buying Works | ApexAuto Malaysia",
    description: `Four steps from choosing a car to collecting it, with how the on-the-road price is made up, the ${holdHours}-hour hold and a monthly estimate.`,
  };
}

export default async function BuyingGuidePage() {
  const holdHours = await readReservationHoldHours();

  return <BuyingJourney holdHours={holdHours} />;
}
