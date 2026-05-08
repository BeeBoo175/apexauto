"use client";

import { useContext, useState } from "react";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { useSettingsWorkspace } from "@/hooks/use-settings-workspace";
import type { SettingsEntry } from "@/hooks/use-settings-client-entry";
import { useStaffSelection } from "@/hooks/use-staff-selection";
import { privateApiFetch } from "@/lib/api-browser";
import {
  parsePromo,
  parseWarranty,
  promoStatuses,
  recordPath,
  settingsSections,
  warrantyStatuses,
} from "@/lib/settings/settings";
import type { SettingsDraft } from "@/lib/settings/settings-draft";
import { MasterDataTable } from "./MasterDataTable";
import { PromoDrawer } from "./PromoDrawer";
import { SettingsForm } from "./SettingsForm";
import { WarrantyDrawer } from "./WarrantyDrawer";
import { promoTableFilters, warrantyTableFilters, type LoadList } from "./settings-filters";
import styles from "@/components/staff/StaffWorklist.module.css";
import workspace from "@/components/staff/StaffWorkspace.module.css";

export function SettingsWorkspace({ entry }: { entry: SettingsEntry }) {
  const state = useSettingsWorkspace(entry);
  const animateEntry = useContext(StaffPageEntranceContext);
  const [draft, setDraft] = useState<SettingsDraft | null>(null);
  const [creating, setCreating] = useState<"promo" | "warranty" | null>(null);
  const promoDrawer = useStaffSelection(entry.promoId, entry.selectedPromo, {
    parameter: "promo",
    path: (id) => recordPath("promos", id),
    parse: parsePromo,
    matches: (value, id) => value.id === id,
    read: privateApiFetch,
    commitAfterRead: true,
  });
  const warrantyDrawer = useStaffSelection(entry.warrantyId, entry.selectedWarranty, {
    parameter: "warranty",
    path: (id) => recordPath("warranties", id),
    parse: parseWarranty,
    matches: (value, id) => value.id === id,
    read: privateApiFetch,
    commitAfterRead: true,
  });
  const { section, settings, promos, warranties, busy } = state;
  const refreshList = () => void state.open(section);
  const loadPromos: LoadList = (filters, page) => state.open("promos", filters, page);
  const loadWarranties: LoadList = (filters, page) => state.open("warranties", filters, page);

  // Section switching sits in the table toolbar, like the other staff tables, and above the form otherwise.
  const tabs = (
    <StaffQueueTabs
      label="Settings sections"
      tabs={settingsSections}
      value={section}
      disabled={busy}
      onChange={(next) => void state.open(next)}
    />
  );

  function unavailable(label: string) {
    return (
      <>
        {tabs}
        <div className={styles.unavailableFilters}>
          <p>{label} are unavailable. Retry when the service is available.</p>
          <Button size="compact" variant="secondary" onClick={() => void state.open(section)}>
            Retry {label.toLowerCase()}
          </Button>
        </div>
      </>
    );
  }

  let body;
  if (section === "settings") {
    body = settings ? (
      <>
        {tabs}
        <SettingsForm
          settings={settings}
          draft={draft}
          busy={busy}
          onDraft={setDraft}
          onSaved={state.setSettings}
          onReload={() => void state.open("settings")}
        />
      </>
    ) : (
      unavailable("Settings")
    );
  } else if (section === "promos") {
    const table = promos
      ? promoTableFilters(promos.filters, busy, promoStatuses, loadPromos)
      : null;
    body =
      promos && table ? (
        <MasterDataTable
          label="Promos"
          noun="promo"
          tabs={tabs}
          snapshot={promos}
          columns={table.columns}
          applied={table.applied}
          busy={busy}
          selectedId={promoDrawer.id}
          onLoad={loadPromos}
          onOpen={(promo) => {
            setCreating(null);
            void promoDrawer.choose(promo.id);
          }}
          onCreate={() => {
            promoDrawer.close();
            setCreating("promo");
          }}
        />
      ) : (
        unavailable("Promos")
      );
  } else {
    const table = warranties
      ? warrantyTableFilters(warranties.filters, busy, warrantyStatuses, loadWarranties)
      : null;
    body =
      warranties && table ? (
        <MasterDataTable
          label="Warranty packages"
          noun="package"
          tabs={tabs}
          snapshot={warranties}
          columns={table.columns}
          applied={table.applied}
          busy={busy}
          selectedId={warrantyDrawer.id}
          onLoad={loadWarranties}
          onOpen={(warranty) => {
            setCreating(null);
            void warrantyDrawer.choose(warranty.id);
          }}
          onCreate={() => {
            warrantyDrawer.close();
            setCreating("warranty");
          }}
        />
      ) : (
        unavailable("Warranty packages")
      );
  }

  return (
    <div className={styles.workspace}>
      <LoadingProgress label="Loading settings" active={busy} />
      <StaffCommandBar
        title="Settings"
        actions={
          <RefreshButton
            label="Refresh settings"
            busy={busy}
            disabled={!state.ready}
            onClick={() => void state.open()}
          />
        }
      />
      <div
        className={`${styles.page} ${animateEntry ? workspace.reveal : ""}`}
        aria-busy={busy}
        inert={!state.ready}
      >
        <div className={styles.body}>{body}</div>
      </div>
      <PromoDrawer
        selection={promoDrawer}
        creating={creating === "promo"}
        busy={busy}
        onCloseCreate={() => setCreating(null)}
        onSaved={refreshList}
      />
      <WarrantyDrawer
        selection={warrantyDrawer}
        creating={creating === "warranty"}
        busy={busy}
        onCloseCreate={() => setCreating(null)}
        onSaved={refreshList}
      />
    </div>
  );
}
