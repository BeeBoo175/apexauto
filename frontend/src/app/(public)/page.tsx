import type { Metadata } from "next";
import { LandingStorefront } from "@/components/vehicles/LandingStorefront";
import { readReservationHoldHours } from "@/lib/guide/guide-reads";

// The hold length comes from the cached purchase guide, so the page is refreshed hourly
// (PUBLIC_CONTENT_REVALIDATE_SECONDS).
export const revalidate = 3600;

export async function generateMetadata(): Promise<Metadata> {
  const holdHours = await readReservationHoldHours();

  return {
    title: "Buy Inspected Used Cars & Sell Online | ApexAuto Malaysia",
    description: `Used cars checked on 210 points at ApexAuto showrooms in Malaysia. Fixed prices, a monthly payment estimate on every car, and a ${holdHours}-hour online hold.`,
  };
}

// The copy is fixed apart from the hold length; the cars, showrooms and finder options load in the browser.
export default async function Home() {
  const holdHours = await readReservationHoldHours();

  return <LandingStorefront holdHours={holdHours} />;
}
