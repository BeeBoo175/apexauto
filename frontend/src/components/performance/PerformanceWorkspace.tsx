"use client";

import { useCallback, useContext, useState } from "react";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { RefreshButton } from "@/components/ui/RefreshButton";
import type { usePerformanceEntry } from "@/hooks/use-performance-entry";
import { useAddressSection } from "@/hooks/use-address-section";
import type { PeriodPreset } from "@/lib/performance/period";
import type { PerformancePeriod } from "@/lib/performance/performance";
import { PerformanceToolbar } from "./PerformanceToolbar";
import { BranchesView, ResultsView } from "./PerformanceViews";
import { TeamView, type TeamExport } from "./TeamView";
import type { TeamTab } from "@/lib/performance/team";
import { defaultTeamUrl } from "@/lib/performance/team-filters";
import workspace from "@/components/staff/StaffWorkspace.module.css";
import styles from "./Performance.module.css";
import dashboard from "@/components/staff/DashboardSection.module.css";
import { writeAddress } from "@/lib/browser-url";

type PerformanceEntry = NonNullable<ReturnType<typeof usePerformanceEntry>["entry"]>;
type View = "results" | "sales" | "workshop" | "branches";

// Period and branch ride in the address beside the open view, so a reload returns to the same page.
function writePeriod(preset: PeriodPreset, period: PerformancePeriod) {
  const url = new URL(window.location.href);
  for (const key of ["period", "from", "to", "branchId"]) {
    url.searchParams.delete(key);
  }
  if (preset !== "last-90") {
    url.searchParams.set("period", preset);
  }
  if (preset === "custom") {
    url.searchParams.set("from", period.from);
    url.searchParams.set("to", period.to);
  }
  if (period.branchId) {
    url.searchParams.set("branchId", period.branchId);
  }
  writeAddress(url);
}

// Performance is the reporting page: how a chosen period went, in tables a manager can read, compare and export.
// What needs a manager today lives on Home.
export function PerformanceWorkspace({ entry }: { entry: PerformanceEntry }) {
  const { actor } = entry;
  const isGeneral = actor.role === "GeneralManager";
  const [preset, setPreset] = useState<PeriodPreset>(entry.preset);
  const [period, setPeriod] = useState(entry.period);
  const [refreshes, setRefreshes] = useState(0);
  const [exports, setExports] = useState<Record<TeamTab, TeamExport | null>>({
    sales: null,
    workshop: null,
  });
  // The same soft entrance as the other staff pages when Performance is navigated to.
  const animateEntry = useContext(StaffPageEntranceContext);
  // Every manager opens on Results; a general manager also has Branches, second.
  const [view, chooseView] = useAddressSection<View>(
    isGeneral ? ["results", "branches", "sales", "workshop"] : ["results", "sales", "workshop"],
    "view",
  );
  const tabs: { value: View; label: string }[] = [
    { value: "results", label: "Results" },
    ...(isGeneral ? [{ value: "branches" as const, label: "Branches" }] : []),
    { value: "sales", label: "Sales & service" },
    { value: "workshop", label: "Workshop" },
  ];
  const current: View = tabs.some((tab) => tab.value === view) ? view : tabs[0].value;
  // Like the other staff pages, a chosen view loads out of sight while the confirmed one stays on screen, and
  // replaces it only once its figures are in, so the page never shows an empty view or moves while loading.
  const [pending, setPending] = useState<View | null>(null);
  const [firstView] = useState(current);
  const team = current === "sales" || current === "workshop";

  const settle = useCallback(
    (settled: View) => {
      if (settled !== pending) {
        return;
      }
      chooseView(settled);
      setPending(null);
    },
    [pending, chooseView],
  );
  const recordExport = useCallback((tab: TeamTab, state: TeamExport | null) => {
    setExports((value) => ({ ...value, [tab]: state }));
  }, []);

  function requestView(next: View) {
    setPending(next === current ? null : next);
  }

  function choose(nextPreset: PeriodPreset, next: PerformancePeriod) {
    setPreset(nextPreset);
    setPeriod(next);
    writePeriod(nextPreset, next);
  }

  // What the entry read belongs only to the view and period the page opened on.
  const opened = (candidate: View) => candidate === firstView && period === entry.period;
  const shared = { period, actor, refreshes };

  function renderView(candidate: View) {
    if (candidate === "results") {
      return (
        <ResultsView
          {...shared}
          initialHeadline={entry.headline}
          initial={opened("results") ? entry.results : { stock: null, stages: null }}
          onSettled={() => settle("results")}
        />
      );
    }
    if (candidate === "branches") {
      return (
        <BranchesView
          {...shared}
          initial={opened("branches") ? entry.comparison : null}
          onSettled={() => settle("branches")}
          onOpen={(branchId) => {
            choose(preset, { ...period, branchId });
            requestView("results");
          }}
        />
      );
    }
    return (
      <TeamView
        tab={candidate}
        initial={opened(candidate) ? entry.team : null}
        urlState={opened(candidate) ? entry.teamUrl : defaultTeamUrl}
        onExport={recordExport}
        onSettled={() => settle(candidate)}
        {...shared}
      />
    );
  }

  const mounted = pending ? [current, pending] : [current];
  return (
    <>
      <StaffCommandBar
        title="Performance"
        actions={
          <RefreshButton
            label="Refresh performance"
            busy={false}
            disabled={false}
            onClick={() => setRefreshes((value) => value + 1)}
          />
        }
      />
      <div className={`${dashboard.workspace} ${animateEntry ? workspace.reveal : ""}`}>
        {/* The period and branch apply to every view, so their row sits above the view tabs. */}
        <div className={dashboard.toolbarFrame}>
          <PerformanceToolbar
            teamExport={team ? (exports[current] ?? "pending") : null}
            preset={preset}
            period={period}
            branches={entry.branches}
            showBranches={isGeneral}
            busy={false}
            onApply={choose}
          />
        </div>
        <StaffQueueTabs
          label="Performance views"
          tabs={tabs}
          value={pending ?? current}
          onChange={requestView}
        />
        {mounted.map((candidate) => (
          <div key={candidate} className={styles.viewSlot} hidden={candidate !== current}>
            {renderView(candidate)}
          </div>
        ))}
      </div>
    </>
  );
}
