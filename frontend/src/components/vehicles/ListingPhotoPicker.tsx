"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import type { useListingEditor } from "@/hooks/use-listing-editor";
import type { useListingPhotos } from "@/hooks/use-listing-photos";
import { moveListingPhoto } from "@/lib/vehicles/listing-photos";
import { ListingGallery, ListingPhoto } from "./ListingGallery";
import { ListingPhotoUpload } from "./ListingPhotoUpload";
import workshop from "@/components/inspection/Workshop.module.css";
import styles from "./ListingGallery.module.css";

export function ListingPhotoPicker({
  vehicleId,
  vehicleName,
  editor,
  photos,
}: {
  vehicleId: string;
  vehicleName: string;
  editor: ReturnType<typeof useListingEditor>;
  photos: ReturnType<typeof useListingPhotos>;
}) {
  const [editing, setEditing] = useState(Boolean(editor.upload.file));
  const { value, operating, change } = editor;
  const { snapshot, busy, failed, load, retry } = photos;
  const ids = value.photoIds;
  const showingEditor = editing || Boolean(editor.upload.file);

  function edit() {
    setEditing(!editing);
    if (!editing && !snapshot) {
      void load();
    }
  }

  return (
    <section className={styles.gallery} aria-label="Listing photos">
      <LoadingProgress label="Loading uploaded listing photos" active={busy} />
      <div className={workshop.detailHeading}>
        <h3>Listing photos</h3>
        <Button
          size="compact"
          variant="ghost"
          disabled={operating}
          aria-expanded={showingEditor}
          onClick={edit}
        >
          {showingEditor ? "Done editing photos" : "Edit photos"}
        </Button>
      </div>
      {showingEditor && (
        <p>
          The first selected photo is the cover. Save the draft to keep this selection and order.
        </p>
      )}
      <ListingGallery
        vehicleId={vehicleId}
        photoIds={ids}
        vehicleName={vehicleName}
        controls={
          showingEditor
            ? (id, index) => (
                <div className={styles.controls}>
                  <Button
                    size="compact"
                    variant="secondary"
                    disabled={operating || index === 0}
                    aria-label={`Move photo ${index + 1} earlier`}
                    onClick={() => change({ ...value, photoIds: moveListingPhoto(ids, id, -1) })}
                  >
                    Move earlier
                  </Button>
                  <Button
                    size="compact"
                    variant="secondary"
                    disabled={operating || index === ids.length - 1}
                    aria-label={`Move photo ${index + 1} later`}
                    onClick={() => change({ ...value, photoIds: moveListingPhoto(ids, id, 1) })}
                  >
                    Move later
                  </Button>
                  <Button
                    size="compact"
                    variant="ghost"
                    disabled={operating}
                    aria-label={`Remove photo ${index + 1}`}
                    onClick={() =>
                      change({ ...value, photoIds: ids.filter((selected) => selected !== id) })
                    }
                  >
                    Remove
                  </Button>
                </div>
              )
            : undefined
        }
      />
      {showingEditor && (
        <section className={styles.gallery} aria-label="Uploaded photo choices">
          <ListingPhotoUpload upload={editor.upload} disabled={operating} />
          <h4>Uploaded photos</h4>
          {failed && (
            <Button size="compact" variant="secondary" disabled={busy} onClick={() => void retry()}>
              Retry uploaded photos
            </Button>
          )}
          {snapshot?.total === 0 && <p>No photos have been uploaded for this vehicle.</p>}
          <div className={styles.photos}>
            {snapshot?.items.map((photo, index) => {
              const position = (snapshot.page - 1) * snapshot.pageSize + index + 1;
              const label = `Uploaded photo ${position}`;
              const selected = ids.includes(photo.id);
              return (
                <div key={photo.id} className={styles.gallery}>
                  {photo.availableAt ? (
                    <ListingPhoto
                      vehicleId={vehicleId}
                      id={photo.id}
                      position={position}
                      vehicleName={vehicleName}
                      label={label}
                      caption={label}
                    />
                  ) : (
                    <p>{label} · Upload pending</p>
                  )}
                  <Button
                    size="compact"
                    variant="secondary"
                    disabled={operating || !photo.availableAt || selected}
                    onClick={() => change({ ...value, photoIds: [...ids, photo.id] })}
                  >
                    {selected ? `${label} selected` : `Select ${label.toLowerCase()}`}
                  </Button>
                </div>
              );
            })}
          </div>
          {snapshot && (
            <nav className={styles.controls} aria-label="Uploaded photo pages">
              <span>
                Page {snapshot.page} of {Math.max(1, Math.ceil(snapshot.total / snapshot.pageSize))}{" "}
                · {snapshot.total} uploaded photos
              </span>
              <Button
                size="compact"
                variant="secondary"
                disabled={busy || snapshot.page <= 1}
                onClick={() => void load(snapshot.page - 1)}
              >
                Previous photos
              </Button>
              <Button
                size="compact"
                variant="secondary"
                disabled={busy || snapshot.page * snapshot.pageSize >= snapshot.total}
                onClick={() => void load(snapshot.page + 1)}
              >
                Next photos
              </Button>
            </nav>
          )}
        </section>
      )}
    </section>
  );
}
