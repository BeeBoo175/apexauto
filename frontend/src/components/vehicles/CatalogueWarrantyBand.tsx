import Image from "next/image";
import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import type { GuideWarranty } from "@/lib/guide/purchase-guide";
import { splitWarrantyName } from "@/lib/guide/warranty-plans";
import styles from "./CatalogueWarrantyBand.module.css";

interface CatalogueWarrantyBandProps {
  warranties: readonly GuideWarranty[];
}

export function CatalogueWarrantyBand({ warranties }: CatalogueWarrantyBandProps) {
  const longestCover = Math.max(...warranties.map((plan) => plan.durationMonths));
  const shortestCover = Math.min(...warranties.map((plan) => plan.durationMonths));

  return (
    <section className={styles.band} aria-labelledby="catalogue-warranty-heading">
      <div className={styles.inner}>
        <Image
          src="/images/warranty-sedan.webp"
          alt=""
          width={389}
          height={182}
          unoptimized
          className={styles.car}
        />

        <div className={styles.copy}>
          <p className={styles.label}>Peace of mind</p>
          <h2 id="catalogue-warranty-heading" className={styles.title}>
            Add ApexShield warranty
          </h2>
          <p className={styles.line}>
            Optional cover from {shortestCover} months, billed separately.
          </p>
        </div>

        <ol className={styles.tiers} aria-label="Warranty plans">
          {warranties.map((plan) => {
            const longest = plan.durationMonths === longestCover;
            return (
              <li key={plan.id} className={styles.tier} data-longest={longest || undefined}>
                {longest && <span className={styles.tierTag}>Longest cover</span>}
                <span className={styles.tierName}>{splitWarrantyName(plan.name).tier}</span>
                <span className={styles.tierTerm}>{plan.durationMonths} mo</span>
              </li>
            );
          })}
        </ol>

        <Link href="/warranty" prefetch={false} className={styles.action}>
          <span>See plans</span>
          <Icon name="next" grid={16} />
        </Link>
      </div>
    </section>
  );
}
