import { useId, type ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { ViewSwitcher } from "@/components/ui/ViewSwitcher";
import type { ListingDraft } from "@/lib/vehicles/listing-draft";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { ListingCardPreview } from "./ListingCardPreview";
import { ListingDetailPreview } from "./ListingDetailPreview";
import workbench from "@/components/staff/StaffWorkspace.module.css";
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
  purchaseSummary,
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
  purchaseSummary?: ReactNode;
}) {
  const fieldsId = useId();

  return (
    <div className={styles.editingWorkspace} data-mode={mode}>
      <div className={`${workbench.stickySectionHeader} ${styles.previewControls}`}>
        <div className={styles.narrowLayoutControl}>
          <ViewSwitcher
            label="Editor view"
            className={styles.layoutSwitch}
            value={mode}
            options={[
              { value: "edit", label: "Fields" },
              { value: "preview", label: "Preview" },
            ]}
            onChange={onMode}
          />
        </div>
        <div className={styles.previewToolbar} role="group" aria-label="Preview controls">
          <ViewSwitcher
            label="Preview representation"
            className={styles.previewSwitch}
            value={target}
            options={[
              { value: "listing", label: "Listing page" },
              { value: "card", label: "Catalogue card" },
            ]}
            onChange={onTarget}
          />
        </div>
      </div>
      <div className={styles.split} data-mode={mode}>
        <section id={fieldsId} className={styles.formPane} aria-label="Listing fields" tabIndex={0}>
          <div className={`${workbench.sectionHeader} ${styles.fieldsHeading}`}>
            <h2 className={workbench.sectionTitle}>Listing details</h2>
          </div>
          {fields}
          <div className={styles.photoPicker}>{photos}</div>
        </section>
        <div className={styles.paneToggleAnchor}>
          <Button
            type="button"
            size="compact"
            variant="secondary"
            className={styles.collapseFields}
            aria-label={mode === "preview" ? "Show listing fields" : "Hide listing fields"}
            title={mode === "preview" ? "Show listing fields" : "Hide listing fields"}
            aria-expanded={mode === "edit"}
            aria-controls={fieldsId}
            onClick={() => onMode(mode === "preview" ? "edit" : "preview")}
          >
            <Icon name={mode === "preview" ? "next" : "previous"} />
          </Button>
        </div>
        <section className={styles.previewPane} aria-label="Listing preview" tabIndex={0}>
          <div className={styles.previewBody}>
            {target === "card" ? (
              <ListingCardPreview snapshot={snapshot} draft={draft} />
            ) : (
              <ListingDetailPreview
                snapshot={snapshot}
                draft={draft}
                signedReport={signedReport}
                purchaseSummary={purchaseSummary}
              />
            )}
          </div>
        </section>
      </div>
    </div>
  );
}
