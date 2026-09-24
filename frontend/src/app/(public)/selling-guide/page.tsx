import type { Metadata } from "next";
import { SellingGuide } from "@/components/guide/SellingGuide";
import { INSPECTION_BAY_MINUTES } from "@/lib/guide/visit-terms";

export const metadata: Metadata = {
  title: "How Selling Works | ApexAuto Malaysia",
  description: `Four short steps online, then one ${INSPECTION_BAY_MINUTES}-minute showroom inspection. See a price range before you sign in and decide once the offer is confirmed.`,
};

export default function SellingGuidePage() {
  return <SellingGuide />;
}
