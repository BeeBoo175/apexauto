"use client";

import { reportDocumentHref } from "@/lib/inspection/contracts";
import { useId, type ReactNode } from "react";
import { StaffLink } from "@/components/staff/StaffLink";
import { formatCurrency } from "@/lib/currency-format";
import { parseMyr } from "@/lib/currency-input";
import { fuelLabel } from "@/lib/vehicles/contracts";
import type { ListingDraft } from "@/lib/vehicles/listing-draft";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { VehicleDetailView } from "./VehicleDetailView";
import { VehicleOverview } from "./VehicleOverview";
import { VehiclePurchaseSummary } from "./VehiclePurchaseSummary";
import layout from "./VehicleLayout.module.css";
import styles from "./ListingEditor.module.css";

export function ListingDetailPreview({
  snapshot,
  draft,
  signedReport = snapshot.currentSignedReport,
  purchaseSummary,
}: {
  snapshot: ListingWorkspace;
  draft: ListingDraft;
  signedReport?: ListingWorkspace["currentSignedReport"];
  purchaseSummary?: ReactNode;
}) {
  const previewId = useId();
  const vehicle = {
    ...snapshot.vehicle,
    transmission: draft.transmission || "Not set",
    fuel: draft.fuel ? fuelLabel(draft.fuel) : "Not set",
  };
  const price = parseMyr(draft.vehiclePrice);
  const roadTax = parseMyr(draft.roadTax);
  const report = signedReport;
  const name = `${vehicle.manufactureYear} ${vehicle.make} ${vehicle.model} ${vehicle.trim}`;

  return (
    <VehicleDetailView
      vehicleId={vehicle.vehicleId}
      name={name}
      branch={vehicle.branchName}
      mileage={`${vehicle.mileageKilometres.toLocaleString("en-MY")} km`}
      transmission={vehicle.transmission}
      fuel={vehicle.fuel}
      price={price === null || price <= 0 ? "Price incomplete" : formatCurrency(price)}
      photos={draft.photoIds.map((id) => ({
        id,
        url: `/api/staff/listings/${vehicle.vehicleId}/photos/${id}`,
      }))}
      summary={
        purchaseSummary ?? (
          <VehiclePurchaseSummary
            total={null}
            breakdown={[
              {
                label: "Road tax",
                amount: roadTax === null ? "Incomplete" : formatCurrency(roadTax),
              },
            ]}
            notice="Historical fees were not recorded."
          />
        )
      }
    >
      <div>
        <div className={styles.previewSection}>
          <VehicleOverview
            vehicle={vehicle}
            description={draft.description.trim() ? draft.description : "Description not set."}
            headingId={`${previewId}-overview`}
          />
        </div>
        <section className={styles.previewSection} aria-labelledby={`${previewId}-inspection`}>
          <h2 id={`${previewId}-inspection`} className={layout.sectionHeading}>
            Signed inspection
          </h2>
          {report ? (
            <StaffLink
              prefetch={false}
              target="_blank"
              rel="noopener"
              href={reportDocumentHref(report.inspectionId, report.revision)}
            >
              Open signed inspection report (new tab)
            </StaffLink>
          ) : (
            <p>No signed inspection report is linked.</p>
          )}
        </section>
      </div>
    </VehicleDetailView>
  );
}
