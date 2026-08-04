"use client";

import { useCallback, useContext, useState } from "react";
import { StaffPageEntranceContext } from "@/components/staff/StaffActorBoundary";
import { StaffCommandBar } from "@/components/staff/StaffPage";
import { StaffQueueTabs } from "@/components/staff/StaffQueueTabs";
import { RefreshButton } from "@/components/ui/RefreshButton";
import type { usePerformanceEntry } from "@/hooks/use-performance-entry";
import { usePerformanceAddressContext } from "@/hooks/use-performance-address";
import type { PeriodPreset } from "@/lib/performance/period";
import type { PerformancePeriod } from "@/lib/performance/performance";
import type { AddressMode } from "@/hooks/use-staff-address";
import { PerformanceToolbar } from "./PerformanceToolbar";
import { BranchesView, ResultsView } from "./PerformanceViews";
import { TeamView, type TeamExport } from "./TeamView";
import type { TeamTab } from "@/lib/performance/team";
import { defaultTeamUrl } from "@/lib/performance/team-filters";
import workspace from "@/components/staff/StaffWorkspace.module.css";
import styles from "./Performance.module.css";
import dashboard from "@/components/staff/DashboardSection.module.css";

type PerformanceEntry = NonNullable<ReturnType<typeof usePerformanceEntry>["entry"]>;
type View = "results" | "sales" | "workshop" | "branches";

// Period and branch ride in the address beside the open view; defaults never appear in it.
function periodValues(preset: PeriodPreset, period: PerformancePeriod) {
  return {
    period: preset === "last90Days" ? "" : preset,
    from: preset === "custom" ? period.from : "",
    to: preset === "custom" ? period.to : "",
    branchId: period.branchId,
  };
}

// What leaving a view takes out of the address: the team table, its open person and the branch measure.
const clearedView = {
  search: "",
  name: "",
  status: "",
  min: "",
  sort: "",
  direction: "",
  page: "",
  pageSize: "",
  person: "",
  section: "",
  measure: "",
};

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
  const address = usePerformanceAddressContext();
  const [view, setView] = useState<string>(address.parameters.view ?? "results");
  const tabs: { value: View; label: string }[] = [
    { value: "results", label: "Results" },
    ...(isGeneral ? [{ value: "branches" as const, label: "Branches" }] : []),
    { value: "sales", label: "Sales & service" },
    { value: "workshop", label: "Workshop" },
  ];
  const current: View = tabs.find((tab) => tab.value === view)?.value ?? tabs[0].value;
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
      setView(settled);
      setPending(null);
      const shown = settled === "sales" || settled === "workshop";
      // A team table writes its own state once it is shown, completing this one push.
      address.patch(
        { ...clearedView, view: settled === "results" ? "" : settled },
        shown ? false : "push",
      );
    },
    [pending, address],
  );
  const recordExport = useCallback((tab: TeamTab, state: TeamExport | null) => {
    setExports((value) => ({ ...value, [tab]: state }));
  }, []);

  function requestView(next: View) {
    setPending(next === current ? null : next);
  }

  function choose(nextPreset: PeriodPreset, next: PerformancePeriod, mode: AddressMode = "push") {
    setPreset(nextPreset);
    setPeriod(next);
    address.patch(periodValues(nextPreset, next), mode);
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
            choose(preset, { ...period, branchId }, false);
            requestView("results");
          }}
        />
      );
    }
    return (
      <TeamView
        tab={candidate}
        active={candidate === current}
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
