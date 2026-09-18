import patterns from "@/components/layout/PublicPatterns.module.css";
import { LiveVehicles } from "@/components/vehicles/LiveVehicles";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import type { FaqEntry } from "@/lib/guide/faq-items";
import type { GuideWarranty } from "@/lib/guide/purchase-guide";
import { WarrantyFaq } from "./WarrantyFaq";
import { WarrantyPlans } from "./WarrantyPlans";
import styles from "./ApexShieldGuide.module.css";

export function ApexShieldGuide({
  warranties,
  faq,
}: {
  warranties: readonly GuideWarranty[];
  faq: readonly FaqEntry[];
}) {
  return (
    <div className={styles.page}>
      <section className={styles.header} aria-labelledby="warranty-title">
        <div className={`${layout.content} ${styles.headerInner} ${patterns.rise}`}>
          <h1 id="warranty-title" className={layout.heroHeading}>
            ApexShield warranty
          </h1>
          <p className={styles.lede}>
            Optional cover for a car you buy from us. The plans a car can take depend on its
            inspection grade, and the price is billed separately from the on-the-road total.
          </p>
        </div>
      </section>

      <section className={styles.plansBand} aria-label="Warranty plans">
        <div className={layout.content}>
          <WarrantyPlans warranties={warranties} />
        </div>
      </section>

      <section className={styles.faqBand} aria-label="Warranty questions">
        <div className={layout.content}>
          <WarrantyFaq entries={faq} />
        </div>
      </section>

      <section className={styles.carsBand} aria-label="Cars for sale">
        <div className={layout.content}>
          <LiveVehicles title="Cars you can cover" />
        </div>
      </section>
    </div>
  );
}
