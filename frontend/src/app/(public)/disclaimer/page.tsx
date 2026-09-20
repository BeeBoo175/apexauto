import type { Metadata } from "next";
import Image from "next/image";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { HelpNotices } from "@/components/guide/HelpNotices";
import layout from "@/components/vehicles/VehicleLayout.module.css";

export const metadata: Metadata = {
  title: "Disclaimers and notices — ApexAuto",
  description:
    "What this demonstration site does and does not do, how its estimates work, and credits for its photographs and icons.",
};

export default function DisclaimerPage() {
  return (
    <>
      <section className={patterns.hero} aria-labelledby="notices-page-title">
        <Image
          src="/images/apexauto-detail.webp"
          alt=""
          fill
          priority
          sizes="100vw"
          className={patterns.heroPhoto}
        />
        <div className={patterns.heroScrim} aria-hidden="true" />
        <div className={`${layout.content} ${patterns.heroInner} ${patterns.rise}`}>
          <h1 id="notices-page-title" className={layout.heroHeading}>
            Disclaimers and notices
          </h1>
          <p className={patterns.heroLede}>
            What this demonstration site does and does not do, how its estimates work, and where its
            photographs and icons come from.
          </p>
        </div>
      </section>
      <HelpNotices />
    </>
  );
}
