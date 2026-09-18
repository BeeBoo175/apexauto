import Image from "next/image";
import Link from "next/link";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { buttonClassName } from "@/components/ui/Button";
import { LiveVehicles } from "@/components/vehicles/LiveVehicles";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import type { FaqEntry } from "@/lib/guide/faq-items";
import type { InspectionGuide } from "@/lib/guide/inspection-points";
import type { GuideWarranty } from "@/lib/guide/purchase-guide";
import { InspectionAreas } from "./InspectionAreas";
import { InspectionGrades } from "./InspectionGrades";
import styles from "./InspectionStandards.module.css";
import { WarrantyFaq } from "./WarrantyFaq";

export function InspectionStandards({
  guide,
  warranties,
  faq,
}: {
  guide: InspectionGuide;
  warranties: readonly GuideWarranty[];
  faq: readonly FaqEntry[];
}) {
  return (
    <>
      <section className={patterns.hero} aria-labelledby="inspection-title">
        <Image
          src="/images/inspect-exterior.jpg"
          alt=""
          fill
          priority
          sizes="100vw"
          className={patterns.heroPhoto}
        />
        <div className={patterns.heroScrim} aria-hidden="true" />
        <div className={`${layout.content} ${patterns.heroInner} ${patterns.rise}`}>
          <h1 id="inspection-title" className={layout.heroHeading}>
            210-point inspection
          </h1>
          <p className={patterns.heroLede}>
            A workshop technician checks every car line by line, repairs what needs repairing before
            it is listed, and signs the record you read before you pay.
          </p>
          <div className={patterns.heroActions}>
            <Link href="/vehicles" prefetch={false} className={buttonClassName("primary", "lg")}>
              Browse inspected cars
            </Link>
          </div>
        </div>
      </section>

      <InspectionAreas points={guide.points} />

      <InspectionGrades policy={guide.grading} warranties={warranties} />

      <section className={styles.carsBand} aria-label="Inspected cars">
        <div className={layout.content}>
          <LiveVehicles title="Cars with a signed record" />
        </div>
      </section>

      <section
        className={`${patterns.band} ${patterns.bandPaper}`}
        aria-label="Questions about inspection"
      >
        <div className={layout.content}>
          <WarrantyFaq entries={faq} />
        </div>
      </section>
    </>
  );
}
