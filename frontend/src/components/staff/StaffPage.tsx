"use client";

import { useContext, type ReactNode, type Ref } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { Card } from "@/components/ui/Card";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffActorContext, StaffNavigationContext } from "./StaffActorBoundary";
import { StaffLoadingShell, type StaffLoadingView } from "./StaffLoadingShell";
import reportStyles from "@/components/inspection/SignedReport.module.css";
import styles from "./Staff.module.css";
import loading from "./StaffLoadingShell.module.css";

const titles: Record<string, string> = {
  "/dashboard/intake": "Intake appointments",
  "/dashboard/inspection": "Workshop",
  "/dashboard/inspection/repairs": "Repair requests",
  "/dashboard/management": "Listings",
};

export function StaffLoading() {
  const actor = useContext(StaffActorContext);
  const pathname = usePathname();
  const search = useSearchParams();
  let workshopView: StaffLoadingView = "technician";
  if (actor && actor.role !== "WorkshopTechnician") {
    workshopView = "inspection";
  }
  const views: Record<string, StaffLoadingView> = {
    "/dashboard/intake": search.get("view") === "list" ? "intake" : "calendar",
    "/dashboard/inspection": workshopView,
    "/dashboard/inspection/repairs": "repairs",
  };
  let view = views[pathname] ?? "inspection";
  let title = titles[pathname] ?? "Your staff workspace";
  if (pathname.startsWith("/dashboard/intake/")) {
    view = "appointment";
    title = "Intake appointment";
  }
  if (pathname.includes("/reports/")) {
    view = "report";
    title = "Signed report";
  }
  return (
    <>
      <LoadingProgress label="Opening workspace" />
      <div className={view === "report" ? reportStyles.controls : undefined}>
        <StaffCommandBar title={actor ? title : null} />
      </div>
      <StaffLoadingShell view={view} selected={Boolean(search.get("inspection"))} />
    </>
  );
}

export function StaffCommandBar({
  title,
  actions,
  actionsRef,
}: {
  title: string | null;
  actions?: ReactNode;
  actionsRef?: Ref<HTMLDivElement>;
}) {
  const navigation = useContext(StaffNavigationContext);
  const pendingTitle = navigation?.pendingPath ? titles[navigation.pendingPath] : undefined;
  return (
    <header className={styles.commandBar}>
      {title === null ? (
        <span className={loading.commandTitle} aria-hidden="true" />
      ) : (
        <h1 tabIndex={-1}>{pendingTitle ?? title}</h1>
      )}
      {(actions || actionsRef) && (
        <div ref={actionsRef} className={styles.actions}>
          {actions}
        </div>
      )}
    </header>
  );
}

export function StaffNotice({ title, children }: { title: string; children: ReactNode }) {
  return (
    <>
      <StaffCommandBar title={title} />
      <Card className={styles.notice}>{children}</Card>
    </>
  );
}
