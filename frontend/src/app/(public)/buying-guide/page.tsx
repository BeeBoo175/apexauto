import type { Metadata } from "next";
import { BuyingJourney } from "@/components/guide/BuyingJourney";

export const metadata: Metadata = {
  title: "How Buying Works | ApexAuto Malaysia",
  description:
    "Four steps from choosing a car to collecting it, with how the on-the-road price is made up, the 48-hour hold and a monthly estimate.",
};

// Everything here is fixed copy and constants, so the page is generated ahead of time. Live prices stay on the car pages.
export default function BuyingGuidePage() {
  return <BuyingJourney />;
}
