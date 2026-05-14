"use client";

import { StaffRecordLoadingShell } from "@/components/staff/StaffRecordLoadingShell";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { VehicleDetailView } from "@/components/vehicles/VehicleDetailView";
import { VehicleOverview } from "@/components/vehicles/VehicleOverview";
import type { useSaleVehicle } from "@/hooks/use-sale-vehicle";
import type { SaleContext } from "@/lib/sales/contracts";
import { formatCurrency } from "@/lib/currency-format";
import { label } from "@/lib/inspection/contracts";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./SaleVehicle.module.css";
import sales from "./Sales.module.css";

const loadingDetails = ["Vehicle", "Listing photos", "Specifications"];

export function SaleVehicle({
  read,
  saleId,
  context,
}: {
  read: ReturnType<typeof useSaleVehicle>;
  saleId: string;
  context: SaleContext;
}) {
  const reference = read.result;
  const heading = (
    <div className={workbench.sectionHeader}>
      <h2 className={workbench.sectionTitle}>Reserved vehicle</h2>
    </div>
  );
  if (!reference) {
    return (
      <section className={sales.section} aria-busy={read.busy}>
        {heading}
        <LoadingProgress label="Loading reserved vehicle listing" active={read.busy} />
        {read.busy ? (
          <StaffRecordLoadingShell label="Loading vehicle listing…" details={loadingDetails} />
        ) : (
          <Button size="compact" variant="secondary" onClick={read.retry}>
            Retry vehicle listing
          </Button>
        )}
      </section>
    );
  }
  const { listing } = reference;
  const vehicle = { ...reference, transmission: listing.transmission, fuel: label(listing.fuel) };
  const name = `${vehicle.manufactureYear} ${vehicle.make} ${vehicle.model} ${vehicle.trim}`;
  return (
    <section className={`${sales.section} ${styles.content}`} aria-busy={read.busy}>
      {heading}
      <LoadingProgress label="Refreshing reserved vehicle" active={read.busy} />
      <p className={styles.context}>
        Vehicle listing retained with this reservation. Agreed pricing is in Deal.
      </p>
      <VehicleDetailView
        layout="reference"
        vehicleId={listing.vehicleId}
        name={name}
        branch={vehicle.branchName}
        mileage={`${vehicle.mileageKilometres.toLocaleString("en-MY")} km`}
        transmission={vehicle.transmission}
        fuel={vehicle.fuel}
        price={formatCurrency(listing.vehiclePrice)}
        priceLabel="Listed price at reservation"
        photos={listing.photoIds.map((id) => ({
          id,
          url: `/api/staff/sales/${saleId}/vehicle/photos/${id}`,
        }))}
        summary={
          <dl className={sales.vehicleFacts}>
            <div>
              <dt>Registration</dt>
              <dd>{context.registration}</dd>
            </div>
            <div>
              <dt>VIN</dt>
              <dd>{context.vin}</dd>
            </div>
          </dl>
        }
      >
        <VehicleOverview vehicle={vehicle} description={listing.description} />
      </VehicleDetailView>
    </section>
  );
}
