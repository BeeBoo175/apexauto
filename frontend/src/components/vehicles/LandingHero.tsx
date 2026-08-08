import Image from "next/image";
import Link from "next/link";
import heroPhoto from "../../../public/images/apexauto-hero.avif";
import { buttonClassName } from "@/components/ui/Button";
import type { Branch } from "@/lib/branches";
import type { Taxonomy } from "@/lib/intake/contracts";
import { LandingHeroWorkbench } from "./LandingHeroWorkbench";
import styles from "./LandingHero.module.css";
import layout from "./VehicleLayout.module.css";

interface LandingHeroProps {
  taxonomy: Taxonomy | null;
  branches: Branch[] | null;
  totalVehicles: number | null;
  onStartTradeInMake: (make: string) => void;
}

export function LandingHero({
  taxonomy,
  branches,
  totalVehicles,
  onStartTradeInMake,
}: LandingHeroProps) {
  const branchSummary =
    branches && branches.length > 0 ? `${branches.length} showrooms` : "Malaysian showrooms";

  return (
    <section className={styles.hero} aria-labelledby="landing-hero-title">
      <div className={styles.media} aria-hidden="true">
        <Image src={heroPhoto} alt="" fill priority sizes="100vw" className={styles.mediaImage} />
        <div className={styles.mediaScrim} />
      </div>
      <div className={`${layout.content} ${styles.inner}`}>
        <div className={styles.copy}>
          <h1 id="landing-hero-title" className={`${layout.heroHeading} ${styles.title}`}>
            Buy inspected cars. Sell yours with clarity.
          </h1>
          <p className={styles.lead}>
            Every ApexAuto vehicle includes a signed 210-point workshop inspection, itemized
            Malaysian pricing and a 48-hour online reservation hold.
          </p>
          <div className={styles.actions}>
            <Link href="/vehicles" prefetch={false} className={buttonClassName("secondary", "lg")}>
              Browse inspected cars
            </Link>
            <Link href="/sell" prefetch={false} className={buttonClassName("shell", "lg")}>
              Sell your car
            </Link>
          </div>
          <dl className={styles.facts}>
            <div>
              <dt>210 points</dt>
              <dd>Signed inspection record open on every car</dd>
            </div>
            <div>
              <dt>MYR 500</dt>
              <dd>48-hour reservation hold with upfront fees</dd>
            </div>
            <div>
              <dt>{branchSummary}</dt>
              <dd>Test drives and trade-ins across Malaysia</dd>
            </div>
          </dl>
        </div>

        <LandingHeroWorkbench
          taxonomy={taxonomy}
          branches={branches}
          totalVehicles={totalVehicles}
          onStartTradeInMake={onStartTradeInMake}
        />
      </div>
    </section>
  );
}
