"use client";

import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffLink } from "@/components/staff/StaffLink";
import { useListingEditor } from "@/hooks/use-listing-editor";
import { useListingPhotos } from "@/hooks/use-listing-photos";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { listingPublication } from "@/lib/vehicles/listings";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { ListingPhotoPicker } from "./ListingPhotoPicker";
import { ListingDraftFields } from "./ListingDraftFields";
import workshop from "@/components/inspection/Workshop.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function ListingEditor({
  snapshot,
  actor,
  section,
  changeSection,
  close,
  onLock,
  onSaved,
}: {
  snapshot: ListingWorkspace;
  actor: CurrentActor;
  section: "listing" | "publication";
  changeSection: (section: "listing" | "publication") => void;
  close: () => void;
  onLock: (locked: boolean) => void;
  onSaved: () => void;
}) {
  const editor = useListingEditor(snapshot, actor, onLock, onSaved);
  const photos = useListingPhotos(snapshot);
  const draft = editor.base;
  const vehicle = {
    ...snapshot.vehicle,
    revisionId: draft?.id ?? null,
    version: draft?.version ?? 0,
  };
  return (
    <div className={workshop.workspace}>
      <LoadingProgress label="Uploading listing photo" active={editor.upload.busy} />
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
            <ListingPhotoPicker
              vehicleId={vehicle.vehicleId}
              vehicleName={`${vehicle.make} ${vehicle.model} ${vehicle.trim}`}
              editor={editor}
              photos={photos}
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
                <dt>Selected public photos</dt>
                <dd>{editor.value.photoIds.length}</dd>
              </div>
            </dl>
            <ListingDraftFields editor={editor} />
            <h3>Signed inspection</h3>
            {snapshot.currentSignedReport ? (
              <StaffLink
                prefetch={false}
                target="_blank"
                rel="noopener"
                href={`/dashboard/inspection/${snapshot.currentSignedReport.inspectionId}/reports/${snapshot.currentSignedReport.revision}`}
              >
                Open signed inspection report (new tab)
              </StaffLink>
            ) : (
              <p>No current signed inspection is available.</p>
            )}
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
          <strong>{editor.stale ? "Newer saved draft available" : "Listing draft"}</strong>
          <span>
            {editor.stale
              ? "Discard local changes to use the newer saved draft."
              : "Save changes before a separate publication approval."}
          </span>
        </div>
        {(editor.dirty || editor.stale) && (
          <Button
            size="compact"
            variant="secondary"
            disabled={editor.operating}
            onClick={editor.discard}
          >
            Discard changes
          </Button>
        )}
        <Button
          size="compact"
          disabled={
            Boolean(editor.upload.file) ||
            editor.command.busy ||
            (!editor.command.uncertain && (!editor.dirty || editor.stale))
          }
          onClick={() => void editor.save()}
        >
          {editor.command.uncertain ? "Reconcile draft save" : "Save draft"}
        </Button>
        <Button size="compact" variant="secondary" disabled={editor.locked} onClick={close}>
          Back to listings
        </Button>
      </footer>
    </div>
  );
}
