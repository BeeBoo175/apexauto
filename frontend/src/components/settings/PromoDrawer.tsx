"use client";

import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { Dialog } from "@/components/ui/Dialog";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import type { useStaffSelection } from "@/hooks/use-staff-selection";
import { useSettingsDrawer } from "@/hooks/use-settings-drawer";
import { parsePromo, type Promo } from "@/lib/settings/settings";
import { PromoForm } from "./PromoForm";
import { SettingsDrawerLoadingShell } from "./SettingsDrawerLoadingShell";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function PromoDrawer({
  selection,
  creating,
  busy,
  onCloseCreate,
  onSaved,
}: {
  selection: ReturnType<typeof useStaffSelection<Promo>>;
  creating: boolean;
  busy: boolean;
  onCloseCreate: () => void;
  onSaved: () => void;
}) {
  const { saver, latest, base, loading, revision, dismiss, close, adoptLatest } = useSettingsDrawer(
    {
      selection,
      creating,
      parse: parsePromo,
      onCloseCreate,
    },
  );

  return (
    <>
      <LoadingProgress label="Loading promo details" active={selection.busy} />
      <LoadingProgress label="Saving promo" active={saver.busy} />
      <Dialog
        open={creating || Boolean(selection.id || selection.pendingId)}
        variant="drawer"
        density="compact"
        title={creating ? "New promo" : (latest?.code ?? "Promo")}
        headerLoading={loading}
        description={creating ? undefined : "Review and change this promo's terms."}
        onClose={close}
        closeDisabled={saver.busy}
        headerActions={
          creating ? undefined : (
            <RefreshButton
              label="Refresh promo details"
              busy={selection.busy}
              disabled={!selection.id || saver.busy}
              onClick={() => void selection.choose(selection.id)}
            />
          )
        }
      >
        {loading && <SettingsDrawerLoadingShell fields={3} />}
        {!creating && !selection.busy && !latest && (
          <div className={workbench.protocol}>
            <ReloadNotice subject="Promo" />
          </div>
        )}
        {(creating || base) && (
          <PromoForm
            key={`${creating ? "new" : base?.id}:${revision}`}
            base={base}
            latest={latest}
            saver={saver}
            busy={busy}
            onSaved={() => {
              onSaved();
              dismiss();
            }}
            onReload={() => void selection.choose(selection.id)}
            onUseLatest={adoptLatest}
            onCancel={dismiss}
          />
        )}
      </Dialog>
    </>
  );
}
