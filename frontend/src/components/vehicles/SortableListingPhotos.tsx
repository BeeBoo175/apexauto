"use client";

import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Input";
import { Icon } from "@/components/ui/Icon";
import { useListingPhotoSort } from "@/hooks/use-listing-photo-sort";
import { reorderListingPhoto } from "@/lib/vehicles/listing-photos";
import { ListingPhoto } from "./ListingGallery";
import styles from "./ListingGallery.module.css";

export function SortableListingPhotos({
  vehicleId,
  vehicleName,
  ids,
  disabled,
  onChange,
}: {
  vehicleId: string;
  vehicleName: string;
  ids: string[];
  disabled: boolean;
  onChange: (ids: string[]) => void;
}) {
  const { grid, visible, active, announcement, start, move, stop, cancel } = useListingPhotoSort(
    ids,
    disabled,
    onChange,
  );
  if (ids.length === 0) {
    return <p className={styles.help}>No photos yet.</p>;
  }

  return (
    <div
      ref={grid}
      className={styles.sortablePhotos}
      role="group"
      aria-label="Selected listing photos"
      onPointerMove={move}
      onPointerUp={(event) => {
        move(event);
        stop(event, true);
      }}
      onPointerCancel={(event) => stop(event, false)}
      onLostPointerCapture={(event) => stop(event, false)}
      onKeyDown={(event) => {
        if (event.key === "Escape" && active) {
          event.preventDefault();
          event.stopPropagation();
          cancel();
        }
      }}
    >
      <span className="sr-only" role="status">
        {announcement}
      </span>
      {visible.map((id, index) => (
        <div
          key={id}
          className={styles.sortablePhoto}
          data-listing-photo-id={id}
          data-dragging={active?.id === id || undefined}
          data-disabled={disabled || ids.length < 2 || undefined}
        >
          <div
            className={styles.dragArea}
            role="group"
            aria-label={`Drag photo ${index + 1} to reorder`}
            onPointerDown={(event) => {
              if (event.target instanceof Element && event.target.closest("button")) {
                return;
              }
              start(event, id);
            }}
            onDragStart={(event) => event.preventDefault()}
          >
            <ListingPhoto
              vehicleId={vehicleId}
              id={id}
              position={index + 1}
              vehicleName={vehicleName}
              caption=""
            />
          </div>
          <div className={styles.sortControls}>
            <Select
              label={`Photo ${index + 1} position`}
              labelHidden
              density="compact"
              value={index}
              disabled={disabled || ids.length < 2 || Boolean(active)}
              onChange={(event) =>
                onChange(reorderListingPhoto(ids, id, Number(event.target.value)))
              }
            >
              {ids.map((_, position) => (
                <option key={position} value={position}>
                  {position === 0 ? "1 · Cover" : `Position ${position + 1}`}
                </option>
              ))}
            </Select>
            <Button
              type="button"
              size="compact"
              variant="ghost"
              aria-label={`Remove photo ${index + 1}`}
              title="Remove photo"
              disabled={disabled || Boolean(active)}
              onClick={() => onChange(ids.filter((selected) => selected !== id))}
            >
              <Icon name="close" />
            </Button>
          </div>
        </div>
      ))}
    </div>
  );
}
