import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import type { PublicVehicle } from "@/lib/vehicles/contracts";
import { catalogueDefaults, catalogueHref } from "@/lib/vehicles/query";
import patterns from "@/components/layout/PublicPatterns.module.css";
import layout from "./VehicleLayout.module.css";
import { VehicleCarousel } from "./VehicleCarousel";
import styles from "./VehicleBrowseMore.module.css";

// A titled carousel band; the detail page uses it for the showroom's cars and for the whole catalogue,
// and the warranty page for the cars an ApexShield plan can cover.
export function VehicleBrowseMore({
  vehicles,
  title = "Browse more",
  href = catalogueHref(catalogueDefaults),
  linkLabel = "See all cars",
  loading = false,
}: {
  vehicles: readonly PublicVehicle[];
  title?: string;
  href?: string;
  linkLabel?: string;
  // The heading and link are final; the row shows card silhouettes until the cars arrive.
  loading?: boolean;
}) {
  if (vehicles.length === 0 && !loading) {
    return null;
  }

  const headingId = `carousel-${title.toLowerCase().replace(/[^a-z0-9]+/g, "-")}`;

  return (
    <section className={styles.section} aria-labelledby={headingId}>
      <div className={styles.head}>
        <h2 id={headingId} className={layout.sectionHeading}>
          {title}
        </h2>
        <Link href={href} prefetch={false} className={patterns.seeAll}>
          <span className={patterns.seeAllLabel}>{linkLabel}</span>
          <Icon name="next" />
        </Link>
      </div>
      <VehicleCarousel vehicles={vehicles} label={title} loading={loading} />
    </section>
  );
}
