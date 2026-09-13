import Image from "next/image";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { StatusPage } from "@/components/ui/StatusPage";
import patterns from "@/components/layout/PublicPatterns.module.css";
import type { ApiResult } from "@/lib/api";
import { formatCurrency } from "@/lib/currency-format";
import type { CataloguePage, PublicVehicle } from "@/lib/vehicles/contracts";
import { catalogueDefaults, catalogueHref } from "@/lib/vehicles/query";
import styles from "./LandingFeaturedVehicles.module.css";
import layout from "./VehicleLayout.module.css";

const SECONDARY_CARD_COUNT = 4;

function vehicleName(vehicle: PublicVehicle) {
  return `${vehicle.manufactureYear} ${vehicle.make} ${vehicle.model} ${vehicle.trim}`;
}

function coverUrl(vehicle: PublicVehicle) {
  return vehicle.coverPhotoId ? `/api/vehicles/${vehicle.id}/photos/${vehicle.coverPhotoId}` : null;
}

function RailCar({ vehicle }: { vehicle: PublicVehicle }) {
  const photoUrl = coverUrl(vehicle);

  return (
    <Link
      href={`/vehicles/${vehicle.id}`}
      prefetch={false}
      className={styles.railCar}
      aria-label={`View details for ${vehicleName(vehicle)}`}
    >
      <span className={styles.railPhoto}>
        {photoUrl && (
          <Image
            src={photoUrl}
            alt=""
            fill
            unoptimized
            sizes="160px"
            className={styles.railImage}
          />
        )}
      </span>
      <span className={styles.railText}>
        <span className={styles.railName}>{vehicleName(vehicle)}</span>
        <span className={styles.railMeta}>
          {vehicle.mileageKilometres.toLocaleString("en-MY")} km · {vehicle.branchName}
        </span>
        <span className={styles.railPrice}>{formatCurrency(vehicle.vehiclePrice)}</span>
      </span>
      <span className={styles.railArrow} aria-hidden="true">
        <Icon name="next" />
      </span>
    </Link>
  );
}

function LeadCar({ vehicle }: { vehicle: PublicVehicle }) {
  const photoUrl = coverUrl(vehicle);
  const fuel = vehicle.fuel === "PlugInHybrid" ? "Plug-in hybrid" : vehicle.fuel;

  return (
    <Link
      href={`/vehicles/${vehicle.id}`}
      prefetch={false}
      className={styles.leadCar}
      aria-label={`View details for ${vehicleName(vehicle)}`}
    >
      {photoUrl && (
        <Image
          src={photoUrl}
          alt={`${vehicleName(vehicle)}, cover photograph`}
          fill
          unoptimized
          priority
          sizes="(max-width: 920px) 100vw, 700px"
          className={styles.leadPhoto}
        />
      )}
      <span className={styles.banner}>
        <i className={styles.pulse} aria-hidden="true" />
        Newest arrival
      </span>
      <div className={styles.leadBody}>
        <span className={styles.leadPrice}>{formatCurrency(vehicle.vehiclePrice)}</span>
        <h3 className={styles.leadName}>{vehicleName(vehicle)}</h3>
        <span>
          {vehicle.mileageKilometres.toLocaleString("en-MY")} km · {vehicle.transmission} · {fuel} ·{" "}
          {vehicle.branchName}
        </span>
      </div>
    </Link>
  );
}

export function LandingFeaturedVehicles({ initial }: { initial: ApiResult<CataloguePage> }) {
  const snapshot = initial.data;
  const lead = snapshot?.items[0] ?? null;
  const followers = snapshot?.items.slice(1, 1 + SECONDARY_CARD_COUNT) ?? [];

  return (
    <section
      className={`${patterns.band} ${styles.section}`}
      aria-labelledby="featured-vehicles-heading"
    >
      <div className={layout.content}>
        <div className={styles.head}>
          <div>
            <h2 id="featured-vehicles-heading" className={patterns.sectionTitle}>
              Latest cars
            </h2>
            <p className={patterns.sectionSub}>Just listed, inspected and fixed-price.</p>
          </div>
          {snapshot && (
            <Link href={catalogueHref(catalogueDefaults)} prefetch={false} className={styles.all}>
              See all {snapshot.total} cars <Icon name="next" />
            </Link>
          )}
        </div>

        {lead && (
          <div className={styles.lot}>
            <LeadCar vehicle={lead} />
            <ul className={styles.rail}>
              {followers.map((vehicle) => (
                <li key={vehicle.id}>
                  <RailCar vehicle={vehicle} />
                </li>
              ))}
            </ul>
          </div>
        )}

        {snapshot && !lead && (
          <div className={styles.empty}>
            <h3 className={layout.cardHeading}>No cars are on sale right now</h3>
            <p>Stock updates as inspections finish. Check the catalogue again soon.</p>
          </div>
        )}

        {!snapshot && (
          <StatusPage
            tone="alert"
            code="503"
            icon="alert-triangle"
            title="Latest cars could not be loaded"
            actions={
              <Link
                href="/vehicles"
                prefetch={false}
                className={buttonClassName("secondary", "md")}
              >
                Open vehicle catalogue
              </Link>
            }
          >
            Reload the page to try again, or continue to the vehicle catalogue.
          </StatusPage>
        )}
      </div>
    </section>
  );
}
