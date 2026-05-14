"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import type { useStaffSelection } from "@/hooks/use-staff-selection";
import { useSettingsSave } from "@/hooks/use-settings-save";
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
  const saver = useSettingsSave(parsePromo);
  const [opened, setOpened] = useState<Promo | null>(null);
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
      <LoadingProgress label="Loading promo details" active={selection.busy} />
      <LoadingProgress label="Saving promo" active={saver.busy} />
      <Dialog
        open={creating || Boolean(selection.id || selection.pendingId)}
        variant="drawer"
        density="compact"
        layout="workspace"
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
            <p>The promo is unavailable. Your table remains unchanged.</p>
            <Button
              size="compact"
              variant="secondary"
              onClick={() => void selection.choose(selection.id)}
            >
              Retry promo
            </Button>
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
