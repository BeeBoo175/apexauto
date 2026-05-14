"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import type { useStaffSelection } from "@/hooks/use-staff-selection";
import { useSettingsSave } from "@/hooks/use-settings-save";
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
  const saver = useSettingsSave(parseWarranty);
  const [opened, setOpened] = useState<Warranty | null>(null);
  const [revision, setRevision] = useState(0);
  const latest = selection.result?.data ?? null;
  // The draft stays based on the first record read for this selection until newer values are chosen.
  if (!creating && latest && opened?.id !== latest.id) {
    setOpened(latest);
  }
  const base = creating || opened?.id !== latest?.id ? null : opened;
  const loading = !creating && selection.busy && !latest;

  function dismiss() {
    setOpened(null);
    if (creating) {
      onCloseCreate();
    } else {
      selection.close();
    }
  }

  function close() {
    if (!saver.busy) {
      dismiss();
    }
  }

  return (
    <>
      <LoadingProgress label="Loading warranty package" active={selection.busy} />
      <LoadingProgress label="Saving warranty package" active={saver.busy} />
      <Dialog
        open={creating || Boolean(selection.id || selection.pendingId)}
        variant="drawer"
        density="compact"
        layout="workspace"
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
            <p>The warranty package is unavailable. Your table remains unchanged.</p>
            <Button
              size="compact"
              variant="secondary"
              onClick={() => void selection.choose(selection.id)}
            >
              Retry package
            </Button>
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
            onUseLatest={() => {
              setOpened(latest);
              setRevision((value) => value + 1);
            }}
            onCancel={dismiss}
          />
        )}
      </Dialog>
    </>
  );
}
