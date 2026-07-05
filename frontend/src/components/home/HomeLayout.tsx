"use client";

import { useContext, type ReactNode } from "react";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import workspace from "@/components/staff/StaffWorkspace.module.css";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import performance from "@/components/performance/Performance.module.css";
import styles from "./Home.module.css";

// The one Home frame every role shares: the command bar, a filter toolbar only where the role has something to
// filter, the band of what needs the user and two panes that scroll on their own. Each role supplies its own parts.
export function HomeLayout({
  busy,
  toolbar,
  onRefresh,
  band,
  left,
  right,
}: {
  busy: boolean;
  toolbar: ReactNode;
  onRefresh: () => void;
  band: ReactNode;
  left: ReactNode;
  right: ReactNode;
}) {
  // The same soft entrance as the other staff pages when Home is navigated to.
  const animateEntry = useContext(StaffPageEntranceContext);
  return (
    <>
      <StaffCommandBar
        title="Home"
        actions={
          <RefreshButton label="Refresh home" busy={false} disabled={false} onClick={onRefresh} />
        }
      />
      <div className={`${performance.workspace} ${animateEntry ? workspace.reveal : ""}`}>
        {toolbar && <div className={performance.toolbarFrame}>{toolbar}</div>}
        <LoadingProgress label="Loading home" active={busy} />
        <div className={styles.view}>
          {band}
          <div className={styles.panes}>
            <div className={styles.pane}>{left}</div>
            <div className={styles.pane}>{right}</div>
          </div>
        </div>
      </div>
    </>
  );
}
