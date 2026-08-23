"use client";

import { useState } from "react";
import { StatusPage } from "@/components/ui/StatusPage";
import { buttonClassName } from "@/components/ui/Button";
import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { useVehiclePromo } from "@/hooks/use-vehicle-promo";
import { useVehicleTradeIn } from "@/hooks/use-vehicle-trade-in";
import { useVehicleDetailSections } from "@/hooks/use-vehicle-detail-sections";
import type { ApiResult } from "@/lib/api";
import { formatCurrency } from "@/lib/currency-format";
import { fuelLabel } from "@/lib/vehicles/contracts";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import type { CustomerVehicleAccess } from "@/lib/vehicles/customer-access";
import { TradeInValuation } from "@/components/sell/TradeInValuation";
import { VehicleReservation } from "./VehicleReservation";
import { VehicleTestDrive } from "./VehicleTestDrive";
import { VehicleDetailView } from "./VehicleDetailView";
import { VehicleDetailSections } from "./VehicleDetailSections";
import { VehiclePurchaseSummary } from "./VehiclePurchaseSummary";
import { VehicleOverview } from "./VehicleOverview";
import { VehiclePromo } from "./VehiclePromo";
import { VehicleFinancing } from "./VehicleFinancing";
import { VehicleInspection } from "./VehicleInspection";
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
  const [reserved, setReserved] = useState(false);
  const price = promo.accepted?.price ?? snapshot?.price;
  const listing = snapshot?.listing;
  const name = listing
    ? `${listing.manufactureYear} ${listing.make} ${listing.model} ${listing.trim}`
    : "Vehicle details";

  return (
    <section className={styles.page}>
      <LoadingProgress
        label="Loading trade-in options"
        active={!tradeIn.taxonomy && tradeIn.busy}
      />
      <div className={styles.topBar}>
        <div className={`${layout.content} ${styles.topBarInner}`}>
          <Link href="/vehicles" prefetch={false} className={styles.backLink}>
            <Icon name="previous" />
            Back to vehicles
          </Link>
          {listing && <span className={styles.showroomName}>{listing.branchName}</span>}
        </div>
      </div>
      {!snapshot || !listing || !price ? (
        <StatusPage
          tone={unavailable ? "info" : "alert"}
          code={unavailable ? "404" : "503"}
          icon={unavailable ? "car" : "alert-triangle"}
          title={
            unavailable
              ? "This vehicle is no longer available"
              : "Vehicle details could not be loaded"
          }
          actions={
            <Link href="/vehicles" prefetch={false} className={buttonClassName("secondary")}>
              Browse the catalogue
            </Link>
          }
        >
          {unavailable
            ? "It may have been reserved or sold. Browse the catalogue for other inspected cars."
            : "The service did not answer. Reload the page to try again."}
        </StatusPage>
      ) : (
        <VehicleDetailView
          vehicleId={id}
          name={name}
          branch={listing.branchName}
          mileage={`${listing.mileageKilometres.toLocaleString("en-MY")} km`}
          transmission={listing.transmission}
          fuel={fuelLabel(listing.fuel)}
          price={formatCurrency(price.vehiclePrice)}
          heroClassName={`${layout.content} ${styles.heroBand}`}
          photos={snapshot.photoIds.map((photoId) => ({
            id: photoId,
            url: `/api/vehicles/${id}/photos/${photoId}`,
          }))}
          summary={
            <VehiclePurchaseSummary
              promo={reserved ? undefined : <VehiclePromo promo={promo} />}
              total={formatCurrency(price.total)}
              totalNote={
                promo.accepted
                  ? `(incl. ${formatCurrency(price.processingFee + price.roadTax + price.serviceTax)} fees, −${formatCurrency(price.promoDiscount)} promo)`
                  : `(incl. ${formatCurrency(price.processingFee + price.roadTax + price.serviceTax)} fees & road tax)`
              }
              breakdown={[
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
                  testDrive={<VehicleTestDrive detail={snapshot} access={reservationAccess} />}
                  onReserved={() => setReserved(true)}
                  financing={
                    <Button variant="ghost" onClick={() => tabs.select("financing")}>
                      Calculate financing
                    </Button>
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
            tradeIn={<TradeInValuation tradeIn={tradeIn} />}
          />
        </VehicleDetailView>
      )}
    </section>
  );
}
