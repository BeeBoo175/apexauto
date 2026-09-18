import type { Metadata } from "next";
import { SellingGuide } from "@/components/guide/SellingGuide";

export const metadata: Metadata = {
  title: "How Selling Works | ApexAuto Malaysia",
  description:
    "Four short steps online, then one 90-minute showroom inspection. See a price range before you sign in and decide once the offer is confirmed.",
};

export default function SellingGuidePage() {
  return <SellingGuide />;
}
