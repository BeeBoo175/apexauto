"use client";

import { useId, useRef, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import type { useListingEditor } from "@/hooks/use-listing-editor";
import type { useListingPhotos } from "@/hooks/use-listing-photos";
import { MAX_LISTING_PHOTOS } from "@/lib/vehicles/listing-photos";
import { ListingPhoto } from "./ListingGallery";
import { SortableListingPhotos } from "./SortableListingPhotos";
import { ListingPhotoUpload } from "./ListingPhotoUpload";
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
  const [libraryOpen, setLibraryOpen] = useState(false);
  const libraryId = useId();
  const existingButton = useRef<HTMLButtonElement>(null);
  const heading = useRef<HTMLHeadingElement>(null);
  const { value, operating, change } = editor;
  const { snapshot, busy, failed, load, retry } = photos;
  const ids = value.photoIds;
  const atLimit = ids.length >= MAX_LISTING_PHOTOS;
  const available = snapshot?.items
    .map((photo, index) => ({
      photo,
      position: (snapshot.page - 1) * snapshot.pageSize + index + 1,
    }))
    .filter(({ photo }) => !ids.includes(photo.id)) ?? [];

  if (atLimit && libraryOpen) {
    setLibraryOpen(false);
  }

  function chooseExisting() {
    setLibraryOpen(!libraryOpen);
    if (!libraryOpen && !snapshot && !busy && !failed) {
      void load();
    }
  }

  function closeLibrary() {
    setLibraryOpen(false);
    (existingButton.current ?? heading.current)?.focus({ preventScroll: true });
  }

  function selectPhoto(id: string) {
    change({ ...value, photoIds: [...ids, id] });
    if (ids.length + 1 === MAX_LISTING_PHOTOS) {
      setLibraryOpen(false);
      heading.current?.focus({ preventScroll: true });
    }
  }

  return (
    <section className={styles.gallery} aria-label="Listing photos">
      <header className={styles.photoHeading}>
        <h3 ref={heading} tabIndex={-1}>
          Photos <span>({ids.length}/{MAX_LISTING_PHOTOS})</span>
        </h3>
        {ids.length > 1 && <span className={styles.help}>Drag to reorder</span>}
      </header>
      <SortableListingPhotos
        vehicleId={vehicleId}
        vehicleName={vehicleName}
        ids={ids}
        disabled={operating}
        onChange={(photoIds) => change({ ...value, photoIds })}
      />
      {(!atLimit || editor.upload.file) && (
        <ListingPhotoUpload upload={editor.upload} disabled={operating || atLimit}>
          <Button
            ref={existingButton}
            size="compact"
            variant="secondary"
            disabled={operating || atLimit}
            aria-expanded={libraryOpen}
            aria-controls={libraryId}
            onClick={chooseExisting}
          >
            Choose existing
          </Button>
        </ListingPhotoUpload>
      )}
      {libraryOpen && !atLimit && (
        <section id={libraryId} className={styles.library} aria-label="Uploaded photos">
          <LoadingProgress label="Loading uploaded listing photos" active={busy} />
          <div className={styles.photoHeading}>
            <h4>Uploaded photos</h4>
            <Button
              size="compact"
              variant="ghost"
              aria-label="Close uploaded photos"
              onClick={closeLibrary}
            >
              <Icon name="close" />
            </Button>
          </div>
          {busy && !snapshot && <p className={styles.help} role="status">Loading photos…</p>}
          {failed && (
            <Button size="compact" variant="secondary" disabled={busy} onClick={() => void retry()}>
              Retry photos
            </Button>
          )}
          {snapshot?.total === 0 && <p className={styles.help}>No uploaded photos.</p>}
          {snapshot && snapshot.total > 0 && available.length === 0 && (
            <p className={styles.help}>No more photos on this page.</p>
          )}
          <div className={styles.libraryPhotos}>
            {available.map(({ photo, position }) => (
              <div key={photo.id} className={styles.libraryChoice}>
                {photo.availableAt ? (
                  <ListingPhoto
                    vehicleId={vehicleId}
                    id={photo.id}
                    position={position}
                    vehicleName={vehicleName}
                    label={`Uploaded photo ${position}`}
                    caption=""
                  />
                ) : (
                  <p className={styles.help}>Upload pending</p>
                )}
                <Button
                  size="compact"
                  variant="secondary"
                  disabled={operating || !photo.availableAt}
                  aria-label={`Select uploaded photo ${position}`}
                  onClick={() => selectPhoto(photo.id)}
                >
                  Select
                </Button>
              </div>
            ))}
          </div>
          {snapshot && snapshot.total > snapshot.pageSize && (
            <nav className={styles.photoActions} aria-label="Uploaded photo pages">
              <span className={styles.help}>{snapshot.page} / {Math.ceil(snapshot.total / snapshot.pageSize)}</span>
              <Button
                size="compact"
                variant="secondary"
                disabled={busy || snapshot.page <= 1}
                onClick={() => void load(snapshot.page - 1)}
              >
                Previous
              </Button>
              <Button
                size="compact"
                variant="secondary"
                disabled={busy || snapshot.page * snapshot.pageSize >= snapshot.total}
                onClick={() => void load(snapshot.page + 1)}
              >
                Next
              </Button>
            </nav>
          )}
        </section>
      )}
    </section>
  );
}
