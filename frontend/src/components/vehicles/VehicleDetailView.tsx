import type { ReactNode } from "react";
import { VehicleDetailGallery, type VehicleDetailPhoto } from "./VehicleDetailGallery";
import styles from "./VehicleDetailView.module.css";
import layoutStyles from "./VehicleLayout.module.css";

export function VehicleDetailView({
  vehicleId,
  name,
  branch,
  mileage,
  transmission,
  fuel,
  price,
  priceLabel = "Vehicle price",
  layout = "purchase",
  heroClassName,
  photos,
  summary,
  children,
}: {
  vehicleId: string;
  name: string;
  branch: string;
  mileage: string;
  transmission: string;
  fuel: string;
  price: string;
  priceLabel?: string;
  layout?: "purchase" | "reference";
  heroClassName?: string;
  photos: VehicleDetailPhoto[];
  summary: ReactNode;
  children: ReactNode;
}) {
  const hero = (
    <div className={styles.hero}>
      <VehicleDetailGallery vehicleId={vehicleId} name={name} photos={photos} />
      <div className={styles.purchase}>
        <header className={styles.identity}>
          <h1 className={layoutStyles.pageHeading}>{name}</h1>
          <p>{branch}</p>
          <dl className={styles.facts}>
            <div>
              <dt>Mileage</dt>
              <dd>{mileage}</dd>
            </div>
            <div>
              <dt>Transmission</dt>
              <dd>{transmission}</dd>
            </div>
            <div>
              <dt>Fuel</dt>
              <dd>{fuel}</dd>
            </div>
          </dl>
        </header>
        <div className={styles.price}>
          <span>{priceLabel}</span>
          <strong>{price}</strong>
        </div>
        {summary}
      </div>
    </div>
  );

  return (
    <div className={styles.view} data-layout={layout}>
      {heroClassName ? <div className={heroClassName}>{hero}</div> : hero}
      {children}
    </div>
  );
}
