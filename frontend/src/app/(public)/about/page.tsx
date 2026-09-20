import type { Metadata } from "next";
import { AboutUs } from "@/components/guide/AboutUs";

export const metadata: Metadata = {
  title: "About ApexAuto | Inspected Used Cars in Malaysia",
  description:
    "The story behind ApexAuto: what we believe about inspecting, repairing and selling used cars, and the milestones along the way.",
};

// Everything here is fixed copy, so the page is generated ahead of time.
export default function AboutPage() {
  return <AboutUs />;
}
