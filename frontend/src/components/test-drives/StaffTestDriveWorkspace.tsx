"use client";

import { useContext } from "react";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import workspace from "@/components/staff/StaffWorkspace.module.css";
import { Dialog } from "@/components/ui/Dialog";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import {
  useStaffTestDriveWorkspace,
  type TestDriveEntry,
} from "@/hooks/use-staff-test-drive-workspace";
import type { CurrentActor } from "@/lib/auth/actor";
import { StaffTestDriveDetail } from "./StaffTestDriveDetail";
import styles from "./StaffTestDrive.module.css";

export function StaffTestDriveWorkspace({
  actor,
  entry,
}: {
  actor: CurrentActor;
  entry: TestDriveEntry;
}) {
  const { list, record, guard, busy, open, close, refresh } = useStaffTestDriveWorkspace(
    actor,
    entry,
  );
  // The same soft entrance as the other staff pages when this page is navigated to.
  const animateEntry = useContext(StaffPageEntranceContext);
  const detail = <StaffTestDriveDetail actor={actor} record={record} refresh={refresh} />;
  const surface = entry.surface;
  return (
    <>
      <LoadingProgress label="Loading test drives" active={busy} />
      <StaffCommandBar
        title="Test drives"
        actions={
          <RefreshButton
            label="Refresh test drives"
            busy={busy}
            disabled={guard.blocked}
            onClick={() => void refresh()}
          />
        }
      />
      <div className={`${styles.page} ${animateEntry ? workspace.reveal : ""}`}>
        {surface.kind === "agent" ? (
          <surface.Component
            list={list}
            selectedId={record.id}
            pendingId={record.pendingId}
            open={open}
          >
            <div className={styles.detailPane}>{detail}</div>
          </surface.Component>
        ) : (
          <>
            <surface.Component
              list={list}
              group={actor.role === "GeneralManager"}
              branches={entry.branches}
              selectedId={record.id}
              open={open}
            />
            <Dialog
              open={Boolean(record.id || record.pendingId)}
              variant="drawer"
              title="Test drive"
              onClose={close}
              headerActions={
                <RefreshButton
                  label="Refresh test drives"
                  busy={busy}
                  disabled={guard.blocked}
                  onClick={() => void refresh()}
                />
              }
            >
              <div className={styles.drawer}>{detail}</div>
            </Dialog>
          </>
        )}
      </div>
    </>
  );
}
