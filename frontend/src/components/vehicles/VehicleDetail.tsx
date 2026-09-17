"use client";

import { useState } from "react";
import { StatusPage } from "@/components/ui/StatusPage";
import { buttonClassName } from "@/components/ui/Button";
import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import { Button } from "@/components/ui/Button";
import { useInitialToast } from "@/hooks/use-initial-toast";
import { useVehiclePromo } from "@/hooks/use-vehicle-promo";
import { useVehicleDetailSections } from "@/hooks/use-vehicle-detail-sections";
import type { ApiResult } from "@/lib/api";
import { formatCurrency, formatWholeCurrency } from "@/lib/currency-format";
import type { ShowroomBranchInfo } from "@/lib/guide/showrooms";
import { fuelLabel, type PublicVehicle } from "@/lib/vehicles/contracts";
import { calculateFinancing, defaultCashContribution } from "@/lib/vehicles/financing";
import { RESERVATION_DEPOSIT } from "@/lib/vehicles/deposit-terms";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import type { CustomerVehicleAccess } from "@/lib/vehicles/customer-access";
import { VehicleReservation } from "./VehicleReservation";
import { VehicleTestDrive } from "./VehicleTestDrive";
import { VehicleDetailGallery } from "./VehicleDetailGallery";
import { VehicleDetailSections } from "./VehicleDetailSections";
import { VehicleKeyFacts } from "./VehicleKeyFacts";
import { VehiclePriceBox } from "./VehiclePriceBox";
import { VehicleMobileBar } from "./VehicleMobileBar";
import { VehicleOverview } from "./VehicleOverview";
import { VehiclePromo } from "./VehiclePromo";
import { VehicleFinancing } from "./VehicleFinancing";
import { VehicleInspection } from "./VehicleInspection";
import { VehicleShowroomSection } from "./VehicleShowroomSection";
import { VehicleBrowseMore } from "./VehicleBrowseMore";
import { VehicleGradeBadge } from "./VehicleGradeBadge";
import styles from "./VehicleDetail.module.css";
import layout from "./VehicleLayout.module.css";

// The loan terms behind the monthly estimate match the calculator's defaults.
const ESTIMATE_YEARS = 7;

export function VehicleDetail({
  id,
  initial,
  reservationAccess,
  showroom,
  moreCars,
}: {
  id: string;
  initial: ApiResult<PublicVehicleDetail>;
  reservationAccess: CustomerVehicleAccess;
  showroom: ShowroomBranchInfo | null;
  moreCars: PublicVehicle[];
}) {
  const snapshot = initial.data;
  const unavailable = initial.status === 404;
  useInitialToast(unavailable || snapshot ? null : initial.message);
  const promo = useVehiclePromo(snapshot);
  const tabs = useVehicleDetailSections();
  const [reserved, setReserved] = useState(false);
  const price = promo.accepted?.price ?? snapshot?.price;
  const listing = snapshot?.listing;
  const name = listing
    ? `${listing.manufactureYear} ${listing.make} ${listing.model} ${listing.trim}`
    : "Vehicle details";

  const monthly = price
    ? calculateFinancing(price.total, Number(defaultCashContribution(price.total)), ESTIMATE_YEARS)
    : null;

  return (
    <section className={styles.page}>
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
        <>
          <div className={`${layout.content} ${styles.listing}`}>
            <div className={styles.main}>
              <VehicleDetailGallery
                vehicleId={id}
                name={name}
                photos={snapshot.photoIds.map((photoId) => ({
                  id: photoId,
                  url: `/api/vehicles/${id}/photos/${photoId}`,
                }))}
                badge={
                  snapshot.checklist.grade ? (
                    <VehicleGradeBadge
                      grade={snapshot.checklist.grade}
                      onSelect={() => tabs.select("inspection")}
                    />
                  ) : null
                }
              />
              <VehicleKeyFacts
                year={listing.manufactureYear}
                mileage={`${listing.mileageKilometres.toLocaleString("en-MY")} km`}
                transmission={listing.transmission}
                fuel={fuelLabel(listing.fuel)}
              />
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
                financing={<VehicleFinancing total={price.total} />}
                showroom={
                  <VehicleShowroomSection showroom={showroom} branchName={listing.branchName} />
                }
              />
            </div>
            <aside className={styles.side} aria-label="Price and reservation">
              <VehiclePriceBox
                name={name}
                showroom={listing.branchName}
                price={formatCurrency(price.vehiclePrice)}
                total={formatCurrency(price.total)}
                totalNote={
                  promo.accepted
                    ? `Includes ${formatCurrency(price.processingFee + price.roadTax + price.serviceTax)} fees, less ${formatCurrency(price.promoDiscount)} promo`
                    : `Includes ${formatCurrency(price.processingFee + price.roadTax + price.serviceTax)} fees and road tax`
                }
                monthly={
                  monthly ? { amount: monthly.monthlyInstallment, years: ESTIMATE_YEARS } : null
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
                          amount: `âˆ’${formatCurrency(price.promoDiscount)}`,
                        },
                      ]
                    : []),
                ]}
                promo={reserved ? null : <VehiclePromo promo={promo} />}
                actions={
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
            </aside>
          </div>
          <div className={layout.content}>
            <VehicleBrowseMore vehicles={moreCars} />
          </div>
          {!reserved && (
            <VehicleMobileBar
              price={formatCurrency(price.vehiclePrice)}
              reserveLabel={`Reserve for ${formatWholeCurrency(RESERVATION_DEPOSIT)}`}
            />
          )}
        </>
      )}
    </section>
  );
}
