import type { ReactNode } from "react";
import { VehicleDetailGallery, type VehicleDetailPhoto } from "./VehicleDetailGallery";
import styles from "./VehicleDetailView.module.css";

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
  photos: VehicleDetailPhoto[];
  summary: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className={styles.view} data-layout={layout}>
      <div className={styles.hero}>
        <VehicleDetailGallery vehicleId={vehicleId} name={name} photos={photos} />
        <div className={styles.purchase}>
          <header className={styles.identity}>
            <h1>{name}</h1>
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
      {children}
    </div>
  );
}
