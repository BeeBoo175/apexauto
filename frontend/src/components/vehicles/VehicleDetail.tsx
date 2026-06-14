"use client";

import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import { Button } from "@/components/ui/Button";
import { LoadingProgress, LoadingProgressReady } from "@/components/ui/LoadingProgress";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { useVehiclePromo } from "@/hooks/use-vehicle-promo";
import { useVehicleTradeIn } from "@/hooks/use-vehicle-trade-in";
import { useVehicleDetailSections } from "@/hooks/use-vehicle-detail-sections";
import type { ApiResult } from "@/lib/api";
import { formatCurrency } from "@/lib/currency-format";
import { fuelLabel } from "@/lib/vehicles/contracts";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import type { CustomerVehicleAccess } from "@/lib/vehicles/customer-access";
import { VehicleReservation } from "./VehicleReservation";
import { VehicleTestDrive } from "./VehicleTestDrive";
import { VehicleDetailView } from "./VehicleDetailView";
import { VehicleDetailSections } from "./VehicleDetailSections";
import { VehiclePurchaseSummary } from "./VehiclePurchaseSummary";
import { VehicleOverview } from "./VehicleOverview";
import { VehiclePromo } from "./VehiclePromo";
import { VehicleFinancing } from "./VehicleFinancing";
import { VehicleInspection } from "./VehicleInspection";
import { VehicleTradeIn } from "./VehicleTradeIn";
import styles from "./VehicleDetail.module.css";
import layout from "./VehicleLayout.module.css";

export function VehicleDetail({
  id,
  initial,
  reservationAccess,
}: {
  id: string;
  initial: ApiResult<PublicVehicleDetail>;
  reservationAccess: CustomerVehicleAccess;
}) {
  const snapshot = initial.data;
  const unavailable = initial.status === 404;
  useInitialToast(unavailable || snapshot ? null : initial.message);
  const promo = useVehiclePromo(snapshot);
  const tradeIn = useVehicleTradeIn();
  const tabs = useVehicleDetailSections();
  const price = promo.accepted?.price ?? snapshot?.price;
  const listing = snapshot?.listing;
  const name = listing
    ? `${listing.manufactureYear} ${listing.make} ${listing.model} ${listing.trim}`
    : "Vehicle details";

  return (
    <section className={`${layout.content} ${styles.page}`}>
      <LoadingProgressReady />
      <LoadingProgress
        label="Loading trade-in options"
        active={!tradeIn.taxonomy && tradeIn.busy}
      />
      <div className={styles.heading}>
        <Link href="/vehicles" prefetch={false}>
          <Icon name="previous" />
          Back to vehicles
        </Link>
      </div>
      {!snapshot || !listing || !price ? (
        <div>
          <h1>Vehicle details</h1>
          <p>
            {unavailable
              ? "This vehicle is currently unavailable. Browse the catalogue for available vehicles."
              : "Vehicle details could not be loaded. Reload the page to try again."}
          </p>
        </div>
      ) : (
        <VehicleDetailView
          vehicleId={id}
          name={name}
          branch={listing.branchName}
          mileage={`${listing.mileageKilometres.toLocaleString("en-MY")} km`}
          transmission={listing.transmission}
          fuel={fuelLabel(listing.fuel)}
          price={formatCurrency(price.vehiclePrice)}
          photos={snapshot.photoIds.map((photoId) => ({
            id: photoId,
            url: `/api/vehicles/${id}/photos/${photoId}`,
          }))}
          summary={
            <VehiclePurchaseSummary
              promo={<VehiclePromo promo={promo} />}
              total={formatCurrency(price.total)}
              breakdown={[
                { label: "Vehicle price", amount: formatCurrency(price.vehiclePrice) },
                { label: "Processing fee", amount: formatCurrency(price.processingFee) },
                { label: "Road tax", amount: formatCurrency(price.roadTax) },
                {
                  label: `SST on processing fee (${snapshot.sstRate * 100}%)`,
                  amount: formatCurrency(price.serviceTax),
                },
                ...(promo.accepted
                  ? [
                      {
                        label: `Promo discount (${promo.accepted.promoCode})`,
                        amount: `−${formatCurrency(price.promoDiscount)}`,
                      },
                    ]
                  : []),
              ]}
              financing={
                <VehicleReservation
                  detail={snapshot}
                  access={reservationAccess}
                  promo={promo.accepted}
                  promoBusy={promo.busy}
                  financing={
                    <>
                      <VehicleTestDrive detail={snapshot} access={reservationAccess} />
                      <Button variant="secondary" onClick={() => tabs.select("financing")}>
                        Calculate financing
                      </Button>
                    </>
                  }
                />
              }
            />
          }
        >
          <VehicleDetailSections
            section={tabs.section}
            select={tabs.select}
            overview={
              <VehicleOverview
                vehicle={{ ...listing, fuel: fuelLabel(listing.fuel) }}
                description={snapshot.description}
              />
            }
            inspection={<VehicleInspection checklist={snapshot.checklist} />}
            financing={
              <VehicleFinancing
                total={price.total}
                tradeInAllowance={tradeIn.accepted?.quote.minValuation ?? 0}
              />
            }
            tradeIn={<VehicleTradeIn tradeIn={tradeIn} />}
          />
        </VehicleDetailView>
      )}
    </section>
  );
}
