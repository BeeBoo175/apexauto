"use client";

import { useContext, useState } from "react";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { EmptyNotice } from "@/components/ui/EmptyNotice";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { useSettingsWorkspace } from "@/hooks/use-settings-workspace";
import type { SettingsEntry } from "@/hooks/use-settings-client-entry";
import { useSettingsAddress } from "@/hooks/use-settings-address";
import { useGuardedAction } from "@/hooks/use-guarded-action";
import { promoStatuses, settingsSections, warrantyStatuses } from "@/lib/settings/settings";
import type { RetentionDraft } from "@/lib/settings/retention";
import type { SettingsDraft } from "@/lib/settings/settings-draft";
import { MasterDataTable } from "./MasterDataTable";
import { PromoDrawer } from "./PromoDrawer";
import { RetentionForm } from "./RetentionForm";
import { SettingsForm } from "./SettingsForm";
import { WarrantyDrawer } from "./WarrantyDrawer";
import { promoTableFilters, warrantyTableFilters, type LoadList } from "./settings-filters";
import styles from "@/components/staff/StaffWorklist.module.css";
import workspace from "@/components/staff/StaffWorkspace.module.css";

export function SettingsWorkspace({ entry }: { entry: SettingsEntry }) {
  const state = useSettingsWorkspace(entry);
  const nav = useSettingsAddress(entry, state);
  const { promoDrawer, warrantyDrawer } = nav;
  const interaction = useGuardedAction();
  const animateEntry = useContext(StaffPageEntranceContext);
  const [draft, setDraft] = useState<SettingsDraft | null>(null);
  const [retentionDraft, setRetentionDraft] = useState<RetentionDraft | null>(null);
  const [editingLocked, setEditingLocked] = useState(false);
  const [creating, setCreating] = useState<"promo" | "warranty" | null>(null);
  const { section, settings, promos, warranties, busy } = state;
  const refreshList = () => void nav.reload();
  const loadList: LoadList = nav.loadList;

  // Section switching sits in the table toolbar, like the other staff tables, and above the form otherwise.
  const tabs = (
    <StaffQueueTabs
      label="Settings sections"
      tabs={settingsSections}
      value={section}
      disabled={busy || interaction.blocked}
      onChange={(next) => {
        interaction.request(() => void nav.switchSection(next));
      }}
    />
  );

  function unavailable(label: string) {
    return (
      <>
        {tabs}
        <div className={styles.unavailableFilters}>
          <ReloadNotice subject={label} />
        </div>
      </>
    );
  }

  // Settings records are created by provisioning, never by this page, so a 404 says one does not exist yet.
  function notSetUp(record: string) {
    return (
      <>
        {tabs}
        <div className={styles.fillNotice}>
          <EmptyNotice icon="table" title="Settings are not set up">
            {`The ${record} record has not been created yet. Reload the page once it has been set up.`}
          </EmptyNotice>
        </div>
      </>
    );
  }

  let body;
  if (section === "settings") {
    if (settings) {
      body = (
        <>
          {tabs}
          <SettingsForm
            settings={settings}
            draft={draft}
            busy={busy}
            onDraft={setDraft}
            onSaved={state.setSettings}
            onLocked={setEditingLocked}
          />
        </>
      );
    } else if (state.settingsMissing) {
      body = notSetUp("purchase settings");
    } else {
      body = unavailable("Settings");
    }
  } else if (section === "retention") {
    if (state.retention) {
      body = (
        <>
          {tabs}
          <RetentionForm
            settings={state.retention}
            draft={retentionDraft}
            busy={busy}
            onDraft={setRetentionDraft}
            onSaved={state.setRetention}
            onLocked={setEditingLocked}
          />
        </>
      );
    } else if (state.retentionMissing) {
      body = notSetUp("retention settings");
    } else {
      body = unavailable("Retention settings");
    }
  } else if (section === "promos") {
    const table = promos ? promoTableFilters(promos.filters, busy, promoStatuses, loadList) : null;
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
          onLoad={loadList}
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
      ? warrantyTableFilters(warranties.filters, busy, warrantyStatuses, loadList)
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
          onLoad={loadList}
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
            disabled={!state.ready || editingLocked}
            onClick={() => void nav.refresh()}
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
