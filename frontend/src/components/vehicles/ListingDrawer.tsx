"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { WorkshopLoadingShell } from "@/components/inspection/WorkshopLoadingShell";
import type { useStaffSelection } from "@/hooks/use-staff-selection";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import type { ApiResult } from "@/lib/api";
import { listingPublication } from "@/lib/vehicles/listings";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { ListingGallery } from "./ListingGallery";
import workshop from "@/components/inspection/Workshop.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function ListingDrawer({
  selection,
  initial,
  initialSection,
}: {
  selection: ReturnType<typeof useStaffSelection<ListingWorkspace>>;
  initial: ApiResult<ListingWorkspace> | null;
  initialSection: "listing" | "publication";
}) {
  const [section, setSection] = useState(initialSection);
  const [entry, setEntry] = useState(initial);
  if (entry !== initial) {
    setEntry(initial);
    setSection(initialSection);
  }
  const snapshot = selection.result?.data;
  const vehicle = snapshot?.vehicle;
  const draft = snapshot?.draft;

  function changeSection(next: "listing" | "publication") {
    setSection(next);
    const url = new URL(window.location.href);
    url.searchParams.set("section", next);
    window.history.replaceState(null, "", url);
  }

  function close() {
    selection.close();
    setSection("listing");
    const url = new URL(window.location.href);
    url.searchParams.delete("section");
    window.history.replaceState(null, "", url);
  }

  return (
    <>
      <LoadingProgress label="Loading listing workspace" active={selection.busy} />
      <Dialog
        open={Boolean(selection.id || selection.pendingId)}
        variant="drawer"
        density="compact"
        width="wide"
        layout="workspace"
        title={vehicle?.registrationPlate ?? "Listing"}
        description={
          vehicle ? `${vehicle.make} ${vehicle.model} · ${vehicle.branchName}` : undefined
        }
        onClose={close}
        headerActions={
          <RefreshButton
            label="Refresh listing"
            busy={selection.busy}
            disabled={!selection.id}
            onClick={() => void selection.choose(selection.id)}
          />
        }
      >
        {selection.busy && !snapshot && <WorkshopLoadingShell label="Loading listing…" />}
        {!selection.busy && !snapshot && (
          <div className={workbench.protocol}>
            <p>The listing is unavailable. Your table remains unchanged.</p>
            <Button
              size="compact"
              variant="secondary"
              onClick={() => void selection.choose(selection.id)}
            >
              Retry listing
            </Button>
          </div>
        )}
        {snapshot && vehicle && (
          <div className={workshop.workspace}>
            <div className={workshop.summary}>
              <div className={workshop.summaryMain}>
                <div>
                  <span className={workshop.summaryLabel}>Saved draft price</span>
                  <strong className={workshop.summaryValue}>
                    {draft ? formatCurrency(draft.vehiclePrice) : "Not prepared"}
                  </strong>
                </div>
                <div className={workshop.summaryContext}>
                  <strong>{listingPublication(vehicle)}</strong>
                  <span>{draft ? `Draft version ${draft.version}` : "No saved draft"}</span>
                </div>
              </div>
            </div>
            <nav className={workshop.sections} aria-label="Listing sections">
              {(["listing", "publication"] as const).map((value) => (
                <Button
                  key={value}
                  size="compact"
                  variant="ghost"
                  className={workshop.sectionButton}
                  aria-pressed={section === value}
                  onClick={() => changeSection(value)}
                >
                  {value === "listing" ? "Listing page" : "Publication"}
                </Button>
              ))}
            </nav>
            <div className={workshop.body}>
              {section === "listing" ? (
                <section className={workbench.stack}>
                  <ListingGallery
                    vehicleId={vehicle.vehicleId}
                    photoIds={draft?.photoIds ?? []}
                    vehicleName={`${vehicle.make} ${vehicle.model} ${vehicle.trim}`}
                  />
                  <h3>
                    {vehicle.manufactureYear} {vehicle.make} {vehicle.model} {vehicle.trim}
                  </h3>
                  <dl className={workshop.facts}>
                    <div>
                      <dt>Mileage</dt>
                      <dd>{vehicle.mileageKilometres.toLocaleString("en-MY")} km</dd>
                    </div>
                    <div>
                      <dt>Inspection status</dt>
                      <dd>
                        {vehicle.vehicleStatus === "PendingInspection"
                          ? "Pending inspection"
                          : vehicle.vehicleStatus}
                      </dd>
                    </div>
                    <div>
                      <dt>Transmission</dt>
                      <dd>{draft?.transmission ?? "Not set"}</dd>
                    </div>
                    <div>
                      <dt>Fuel</dt>
                      <dd>
                        {draft?.fuel === "PlugInHybrid"
                          ? "Plug-in hybrid"
                          : (draft?.fuel ?? "Not set")}
                      </dd>
                    </div>
                    <div>
                      <dt>Road tax</dt>
                      <dd>{draft ? formatCurrency(draft.roadTax) : "Not set"}</dd>
                    </div>
                    <div>
                      <dt>Selected public photos</dt>
                      <dd>{draft?.photoIds.length ?? 0}</dd>
                    </div>
                  </dl>
                  <h3>Description</h3>
                  <p>{draft?.description ?? "No listing description has been saved."}</p>
                </section>
              ) : (
                <section className={workbench.stack}>
                  <h3>Publication record</h3>
                  <dl className={workshop.facts}>
                    <div>
                      <dt>Availability</dt>
                      <dd>{listingPublication(vehicle)}</dd>
                    </div>
                    <div>
                      <dt>Current signed inspection</dt>
                      <dd>
                        {snapshot.currentSignedRevisionId
                          ? "Signed inspection recorded"
                          : "No current signed inspection"}
                      </dd>
                    </div>
                    <div>
                      <dt>Last approved</dt>
                      <dd>
                        {snapshot.publication
                          ? formatMalaysianDateTime(snapshot.publication.recordedAt)
                          : "Not approved"}
                      </dd>
                    </div>
                    <div>
                      <dt>Approved price</dt>
                      <dd>
                        {snapshot.approvedRevision
                          ? formatCurrency(snapshot.approvedRevision.vehiclePrice)
                          : "Not approved"}
                      </dd>
                    </div>
                  </dl>
                  {snapshot.approvedRevision && (
                    <>
                      <h3>Approved description</h3>
                      <p>{snapshot.approvedRevision.description}</p>
                    </>
                  )}
                  {snapshot.withdrawal && (
                    <>
                      <h3>Withdrawal reason</h3>
                      <p>{snapshot.withdrawal.reason}</p>
                      <p>{formatMalaysianDateTime(snapshot.withdrawal.recordedAt)}</p>
                    </>
                  )}
                </section>
              )}
            </div>
            <footer className={workshop.footer}>
              <div className={workshop.footerContext}>
                <strong>Listing review</strong>
                <span>Saved listing and publication details are read only.</span>
              </div>
              <Button size="compact" variant="secondary" onClick={close}>
                Back to listings
              </Button>
            </footer>
          </div>
        )}
      </Dialog>
    </>
  );
}
