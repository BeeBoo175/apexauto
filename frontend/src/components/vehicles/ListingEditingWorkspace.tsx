import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import type { ListingDraft } from "@/lib/vehicles/listing-draft";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { ListingCardPreview } from "./ListingCardPreview";
import { ListingDetailPreview } from "./ListingDetailPreview";
import styles from "./ListingEditor.module.css";

export function ListingEditingWorkspace({
  snapshot,
  draft,
  fields,
  photos,
  target,
  onTarget,
  mode,
  onMode,
  signedReport = snapshot.currentSignedReport,
}: {
  snapshot: ListingWorkspace;
  draft: ListingDraft;
  fields: ReactNode;
  photos: ReactNode;
  target: "listing" | "card";
  onTarget: (target: "listing" | "card") => void;
  mode: "edit" | "preview";
  onMode: (mode: "edit" | "preview") => void;
  signedReport?: ListingWorkspace["currentSignedReport"];
}) {
  return (
    <div>
      <nav className={styles.modes} aria-label="Listing editor mode">
        <Button
          size="compact"
          variant="secondary"
          aria-pressed={mode === "edit"}
          onClick={() => onMode("edit")}
        >
          Fields
        </Button>
        <Button
          size="compact"
          variant="secondary"
          aria-pressed={mode === "preview"}
          onClick={() => onMode("preview")}
        >
          Preview
        </Button>
      </nav>
      <div className={styles.split} data-mode={mode}>
        <section className={styles.formPane} aria-label="Listing fields">
          {fields}
          <div className={styles.photoPicker}>{photos}</div>
        </section>
        <section className={styles.previewPane} aria-label="Listing preview">
          <nav className={styles.previewToolbar} aria-label="Preview controls">
            <div className={styles.previewTargets} role="group" aria-label="Preview representation">
              <Button
                size="compact"
                variant="secondary"
                aria-pressed={target === "listing"}
                onClick={() => onTarget("listing")}
              >
                Listing page
              </Button>
              <Button
                size="compact"
                variant="secondary"
                aria-pressed={target === "card"}
                onClick={() => onTarget("card")}
              >
                Catalogue card
              </Button>
            </div>
            <Button
              size="compact"
              variant="ghost"
              className={styles.expandPreview}
              aria-pressed={mode === "preview"}
              onClick={() => onMode(mode === "preview" ? "edit" : "preview")}
            >
              {mode === "preview" ? "Show fields" : "Expand preview"}
            </Button>
          </nav>
          <div className={styles.previewBody}>
            {target === "card" ? (
              <ListingCardPreview snapshot={snapshot} draft={draft} />
            ) : (
              <ListingDetailPreview snapshot={snapshot} draft={draft} signedReport={signedReport} />
            )}
          </div>
        </section>
      </div>
    </div>
  );
}
