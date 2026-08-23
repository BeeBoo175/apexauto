"use client";

import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { Dialog } from "@/components/ui/Dialog";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import type { useStaffSelection } from "@/hooks/use-staff-selection";
import { useSettingsDrawer } from "@/hooks/use-settings-drawer";
import { parseWarranty, type Warranty } from "@/lib/settings/settings";
import { SettingsDrawerLoadingShell } from "./SettingsDrawerLoadingShell";
import { WarrantyForm } from "./WarrantyForm";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function WarrantyDrawer({
  selection,
  creating,
  busy,
  onCloseCreate,
  onSaved,
}: {
  selection: ReturnType<typeof useStaffSelection<Warranty>>;
  creating: boolean;
  busy: boolean;
  onCloseCreate: () => void;
  onSaved: () => void;
}) {
  const { saver, latest, base, loading, revision, dismiss, close, adoptLatest } = useSettingsDrawer(
    {
      selection,
      creating,
      parse: parseWarranty,
      onCloseCreate,
    },
  );

  return (
    <>
      <LoadingProgress label="Loading warranty package" active={selection.busy} />
      <LoadingProgress label="Saving warranty package" active={saver.busy} />
      <Dialog
        open={creating || Boolean(selection.id || selection.pendingId)}
        variant="drawer"
        density="compact"
        title={creating ? "New warranty package" : (latest?.name ?? "Warranty package")}
        headerLoading={loading}
        description={creating ? undefined : "Review and change this package's terms."}
        onClose={close}
        closeDisabled={saver.busy}
        headerActions={
          creating ? undefined : (
            <RefreshButton
              label="Refresh warranty package"
              busy={selection.busy}
              disabled={!selection.id || saver.busy}
              onClick={() => void selection.choose(selection.id)}
            />
          )
        }
      >
        {loading && <SettingsDrawerLoadingShell fields={5} />}
        {!creating && !selection.busy && !latest && (
          <div className={workbench.protocol}>
            <ReloadNotice subject="Package" />
          </div>
        )}
        {(creating || base) && (
          <WarrantyForm
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
