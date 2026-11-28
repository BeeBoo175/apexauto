import Image from "next/image";
import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import { PhotoPlaceholder } from "@/components/ui/PhotoPlaceholder";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { usePhotoLoading } from "@/hooks/use-photo-loading";
import patterns from "@/components/layout/PublicPatterns.module.css";
import type { ApiResult } from "@/lib/api";
import { formatCurrency } from "@/lib/currency-format";
import { fuelLabel, type CataloguePage, type PublicVehicle } from "@/lib/vehicles/contracts";
import { catalogueDefaults, catalogueHref } from "@/lib/vehicles/query";
import { vehicleTitle } from "@/lib/vehicles/vehicle-title";
import styles from "./LandingFeaturedVehicles.module.css";
import layout from "./VehicleLayout.module.css";

const SECONDARY_CARD_COUNT = 4;

// Stand-in cars for the loading silhouette: the real cards laid out with placeholder text, so each has its final height.
const SHELL_VEHICLE: PublicVehicle = {
  id: "shell",
  branchId: "shell",
  branchName: "ApexAuto Petaling Jaya",
  make: "Make name",
  model: "Model name",
  trim: "Trim level",
  manufactureYear: 2024,
  mileageKilometres: 50000,
  vehiclePrice: 50000,
  onTheRoadTotal: 50630,
  transmission: "Automatic",
  fuel: "Petrol",
  grade: "A",
  coverPhotoId: null,
  photoCount: 0,
  publishedAt: "2024-01-01T00:00:00Z",
};

function coverUrl(vehicle: PublicVehicle) {
  return vehicle.coverPhotoId ? `/api/vehicles/${vehicle.id}/photos/${vehicle.coverPhotoId}` : null;
}

function RailCar({ vehicle, shell = false }: { vehicle: PublicVehicle; shell?: boolean }) {
  const photoUrl = coverUrl(vehicle);
  const photo = usePhotoLoading(photoUrl);

  return (
    <Link
      href={`/vehicles/${vehicle.id}`}
      prefetch={false}
      className={styles.railCar}
      data-shell={shell || undefined}
      tabIndex={shell ? -1 : undefined}
      aria-hidden={shell || undefined}
      aria-label={shell ? undefined : `View details for ${vehicleTitle(vehicle)}`}
    >
      <span className={styles.railPhoto}>
        <PhotoPlaceholder loading={photo.loading || shell} />
        {photoUrl && (
          <Image
            src={photoUrl}
            alt=""
            fill
            unoptimized
            sizes="160px"
            className={styles.railImage}
            {...photo.imageProps}
          />
        )}
      </span>
      <span className={styles.railText}>
        <span className={styles.railName}>{vehicleTitle(vehicle)}</span>
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

function LeadCar({ vehicle, shell = false }: { vehicle: PublicVehicle; shell?: boolean }) {
  const photoUrl = coverUrl(vehicle);
  const photo = usePhotoLoading(photoUrl);
  const fuel = fuelLabel(vehicle.fuel);

  return (
    <Link
      href={`/vehicles/${vehicle.id}`}
      prefetch={false}
      className={styles.leadCar}
      data-shell={shell || undefined}
      tabIndex={shell ? -1 : undefined}
      aria-hidden={shell || undefined}
      aria-label={shell ? undefined : `View details for ${vehicleTitle(vehicle)}`}
    >
      <PhotoPlaceholder loading={photo.loading} />
      {photoUrl && (
        <Image
          src={photoUrl}
          alt={`${vehicleTitle(vehicle)}, cover photograph`}
          fill
          unoptimized
          priority
          sizes="(max-width: 920px) 100vw, 700px"
          className={styles.leadPhoto}
          {...photo.imageProps}
        />
      )}
      <span className={styles.banner}> Newest listing</span>
      <div className={styles.leadBody}>
        <span className={styles.leadPrice}>{formatCurrency(vehicle.vehiclePrice)}</span>
        <h3 className={styles.leadName}>{vehicleTitle(vehicle)}</h3>
        <span className={styles.leadMeta}>
          {vehicle.mileageKilometres.toLocaleString("en-MY")} km · {vehicle.transmission} · {fuel} ·{" "}
          {vehicle.branchName}
        </span>
      </div>
    </Link>
  );
}

export function LandingFeaturedVehicles({ result }: { result: ApiResult<CataloguePage> | null }) {
  // Null while the cars load: the same cards show with placeholder text.
  const loading = result === null;
  const snapshot = result?.data ?? null;
  const lead = loading ? SHELL_VEHICLE : (snapshot?.items[0] ?? null);
  const followers = loading
    ? Array.from({ length: SECONDARY_CARD_COUNT }, (_, index) => ({
        ...SHELL_VEHICLE,
        id: `shell-${index}`,
      }))
    : (snapshot?.items.slice(1, 1 + SECONDARY_CARD_COUNT) ?? []);

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
            <p className={patterns.sectionSub}>The newest cars across our showrooms.</p>
          </div>
          {(snapshot || loading) && (
            <Link
              href={catalogueHref(catalogueDefaults)}
              prefetch={false}
              className={`${patterns.seeAll} ${styles.all}`}
              data-shell={loading || undefined}
            >
              <span className={`${patterns.seeAllLabel} ${styles.allLabel}`}>
                See all {snapshot?.total ?? 100} cars
              </span>{" "}
              <Icon name="next" />
            </Link>
          )}
        </div>

        {lead && (
          <div className={styles.lot}>
            <LeadCar vehicle={lead} shell={loading} />
            <ul className={styles.rail}>
              {followers.map((vehicle) => (
                <li key={vehicle.id}>
                  <RailCar vehicle={vehicle} shell={loading} />
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

        {!loading && !snapshot && <ReloadNotice subject="Latest cars" compact />}
      </div>
    </section>
  );
}
