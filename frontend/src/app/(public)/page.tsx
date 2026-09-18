import type { Metadata } from "next";
import { LandingStorefront } from "@/components/vehicles/LandingStorefront";
import { RESERVATION_HOLD_HOURS } from "@/lib/vehicles/deposit-terms";

export const metadata: Metadata = {
  title: "Buy Inspected Used Cars & Sell Online | ApexAuto Malaysia",
  description: `Used cars checked on 210 points at ApexAuto showrooms in Malaysia. Fixed prices, a monthly payment estimate on every car, and a ${RESERVATION_HOLD_HOURS}-hour online hold.`,
};

// Everything on this page is fixed copy; the cars, showrooms and finder options load in the browser, so the page
// itself is generated ahead of time.
export default function Home() {
  return <LandingStorefront />;
}
