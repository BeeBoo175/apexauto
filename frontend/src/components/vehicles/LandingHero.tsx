import Image from "next/image";
import { RESERVATION_HOLD_HOURS } from "@/lib/vehicles/deposit-terms";
import type { Branch } from "@/lib/branches";
import type { Taxonomy } from "@/lib/intake/contracts";
import patterns from "@/components/layout/PublicPatterns.module.css";
import { LandingFinder } from "./LandingFinder";
import styles from "./LandingHero.module.css";
import layout from "./VehicleLayout.module.css";

interface LandingHeroProps {
  taxonomy: Taxonomy | null;
  branches: Branch[] | null;
  totalVehicles: number | null;
}

export function LandingHero({ taxonomy, branches, totalVehicles }: LandingHeroProps) {
  return (
    <section className={styles.hero} aria-labelledby="landing-hero-title">
      <div className={styles.media} aria-hidden="true">
        <Image
          src="/images/hero-night-showroom.jpg"
          alt=""
          fill
          priority
          sizes="100vw"
          className={styles.mediaImage}
        />
        <div className={styles.mediaScrim} />
      </div>
      <div className={`${layout.content} ${styles.inner}`}>
        <div className={`${styles.copy} ${patterns.rise}`}>
          <h1 id="landing-hero-title" className={`${layout.heroHeading} ${styles.title}`}>
            Buy inspected cars. Sell yours with clarity.
          </h1>
          <p className={styles.lead}>
            A signed 210-point inspection, one fixed price and a {RESERVATION_HOLD_HOURS}-hour hold
            on every car.
          </p>
        </div>
        <div className={`${styles.finderRow} ${patterns.rise}`}>
          <LandingFinder taxonomy={taxonomy} branches={branches} totalVehicles={totalVehicles} />
        </div>
      </div>
    </section>
  );
}
