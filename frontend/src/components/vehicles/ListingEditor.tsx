"use client";

import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffLink } from "@/components/staff/StaffLink";
import { useListingEditor } from "@/hooks/use-listing-editor";
import { useListingPhotos } from "@/hooks/use-listing-photos";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { listingPublication, listingSections, type ListingSection } from "@/lib/vehicles/listings";
import { formatCurrency } from "@/lib/currency-format";
import { ListingPublication } from "./ListingPublication";
import { ListingPhotoPicker } from "./ListingPhotoPicker";
import { ListingDraftFields } from "./ListingDraftFields";
import { ListingCardPreview } from "./ListingCardPreview";
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
  section: ListingSection;
  changeSection: (section: ListingSection) => void;
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
      <LoadingProgress label="Approving publication" active={editor.approval.busy} />
      <LoadingProgress label="Withdrawing publication" active={editor.withdrawal.busy} />
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
        {listingSections.map((value) => (
          <Button
            key={value}
            size="compact"
            variant="ghost"
            className={workshop.sectionButton}
            aria-pressed={section === value}
            onClick={() => changeSection(value)}
          >
            {{ listing: "Listing page", card: "Catalogue card", publication: "Publication" }[value]}
          </Button>
        ))}
      </nav>
      <div className={workshop.body}>
        {section === "listing" && (
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
        )}
        {section === "card" && <ListingCardPreview snapshot={snapshot} draft={editor.value} />}
        {section === "publication" && <ListingPublication snapshot={snapshot} editor={editor} />}
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
            editor.approval.busy ||
            editor.approval.uncertain ||
            editor.withdrawal.busy ||
            editor.withdrawal.uncertain ||
            editor.command.busy ||
            (!editor.command.uncertain && (!editor.dirty || editor.stale))
          }
          onClick={() => void editor.save()}
        >
          {editor.command.uncertain ? "Reconcile draft save" : "Save draft"}
        </Button>
        {(section === "publication" || editor.approval.busy || editor.approval.uncertain) && (
          <Button
            size="compact"
            disabled={editor.approval.busy || (!editor.approval.uncertain && !editor.canApprove)}
            onClick={() => void editor.approve()}
          >
            {editor.approval.uncertain ? "Reconcile publication approval" : "Approve publication"}
          </Button>
        )}
        {(section === "publication" || editor.withdrawal.busy || editor.withdrawal.uncertain) && (
          <Button
            size="compact"
            variant="danger"
            disabled={
              editor.withdrawal.busy || (!editor.withdrawal.uncertain && !editor.canWithdraw)
            }
            onClick={() => void editor.withdraw()}
          >
            {editor.withdrawal.uncertain ? "Reconcile listing withdrawal" : "Withdraw listing"}
          </Button>
        )}
        <Button size="compact" variant="secondary" disabled={editor.locked} onClick={close}>
          Back to listings
        </Button>
      </footer>
    </div>
  );
}
